using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Security;
using Synentra.Domain.Agents;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

namespace Synentra.Infrastructure.Security;

public sealed class JwtAgentAuthenticator : IAgentAuthenticator
{
    private readonly AgentAuthConfiguration _options;
    private readonly ITokenService _selfSignedService;
    private readonly Lazy<ConfigurationManager<OpenIdConnectConfiguration>> _oidcConfigManager;

    public JwtAgentAuthenticator(IOptions<SecurityConfiguration> options, ITokenService selfSignedService)
    {
        // Fall back to a default SelfSigned configuration if none is provided.
        var agentAuth = options?.Value?.AgentAuth;
        if (agentAuth == null)
        {
            agentAuth = new AgentAuthConfiguration
            {
                Provider = AgentAuthProviderType.SelfSigned
                // All other properties remain default (null/empty).
            };
        }

        _options = agentAuth;
        _selfSignedService = selfSignedService;

        // The Lazy is only evaluated when Provider != SelfSigned.
        // If Jwt config is missing, an exception will be thrown at that point,
        // which is acceptable because external providers require configuration.
        _oidcConfigManager = new Lazy<ConfigurationManager<OpenIdConnectConfiguration>>(() =>
        {
            var jwt = _options.Jwt;
            if (jwt == null)
            {
                throw new InvalidOperationException(
                    "JWT configuration is missing. To use an external identity provider, " +
                    "configure Security:AgentAuth:Jwt.");
            }

            var metadataUrl = !string.IsNullOrWhiteSpace(jwt.MetadataUrl)
                ? jwt.MetadataUrl
                : $"{jwt.Authority.TrimEnd('/')}/.well-known/openid-configuration";

            return new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataUrl,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever
                {
                    RequireHttps = !metadataUrl.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                });
        });
    }

    public AgentAuthResult Authenticate(Agent agent)
    {
        if (_options.Provider != AgentAuthProviderType.SelfSigned)
            return AgentAuthResult.Failure(
                "Token generation is not supported for external JWT providers. " +
                "Obtain a token from the configured identity provider.");

        var token = _selfSignedService.GenerateToken(agent);
        return AgentAuthResult.Success(token);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string credential, CancellationToken cancellationToken = default)
    {
        return _options.Provider == AgentAuthProviderType.SelfSigned
            ? _selfSignedService.ValidateToken(credential)
            : await ValidateExternalTokenAsync(credential, cancellationToken);
    }

    private async Task<ClaimsPrincipal?> ValidateExternalTokenAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var oidcConfig = await _oidcConfigManager.Value.GetConfigurationAsync(cancellationToken);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidcConfig.SigningKeys,
                ValidateIssuer = _options.Jwt!.ValidateIssuer,
                ValidIssuer = _options.Jwt.ValidateIssuer ? _options.Jwt.Authority : null,
                ValidateAudience = _options.Jwt.ValidateAudience,
                ValidAudience = _options.Jwt.ValidateAudience ? _options.Jwt.Audience : null,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }
}