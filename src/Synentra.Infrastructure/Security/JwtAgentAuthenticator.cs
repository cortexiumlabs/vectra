using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly Lazy<ITokenService> _selfSignedService;
    private readonly Lazy<ConfigurationManager<OpenIdConnectConfiguration>> _oidcConfigManager;

    public JwtAgentAuthenticator(IOptions<SecurityConfiguration> options, IServiceProvider serviceProvider)
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
        _selfSignedService = new Lazy<ITokenService>(() => serviceProvider.GetRequiredService<ITokenService>());

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
        var token = _selfSignedService.Value.GenerateToken(agent);
        return AgentAuthResult.Success(token);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string credential, CancellationToken cancellationToken = default)
    {
        if (_options.Provider == AgentAuthProviderType.SelfSigned)
            return _selfSignedService.Value.ValidateToken(credential);

        var externalPrincipal = await ValidateExternalTokenAsync(credential, cancellationToken);
        if (externalPrincipal is not null)
            return externalPrincipal;

        try
        {
            return _selfSignedService.Value.ValidateToken(credential);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<ClaimsPrincipal?> ValidateExternalTokenAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var oidcConfig = await _oidcConfigManager.Value.GetConfigurationAsync(cancellationToken);
            var jwt = _options.Jwt!;

            // Start with minimal validation
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidcConfig.SigningKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // Conditionally enable issuer validation only if Authority is provided
            if (!string.IsNullOrWhiteSpace(jwt.Authority) && jwt.ValidateIssuer)
            {
                validationParameters.ValidateIssuer = true;
                validationParameters.ValidIssuer = jwt.Authority;
            }
            else
            {
                validationParameters.ValidateIssuer = false;
            }

            // Conditionally enable audience validation only if Audience is provided
            if (!string.IsNullOrWhiteSpace(jwt.Audience) && jwt.ValidateAudience)
            {
                validationParameters.ValidateAudience = true;
                validationParameters.ValidAudience = jwt.Audience;
            }
            else
            {
                validationParameters.ValidateAudience = false;
            }

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