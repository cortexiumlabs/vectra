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
using Microsoft.Extensions.Logging;

namespace Synentra.Infrastructure.Security;

public sealed class JwtAgentAuthenticator : IAgentAuthenticator
{
    private readonly AgentAuthConfiguration _options;
    private readonly Lazy<ITokenService> _tokenService;
    private readonly Lazy<ConfigurationManager<OpenIdConnectConfiguration>> _oidcConfigManager;
    private readonly ILogger<JwtAgentAuthenticator> _logger;

    public JwtAgentAuthenticator(
        IOptions<SecurityConfiguration> options, 
        IServiceProvider serviceProvider)
    {
        _options = options?.Value?.AgentAuth ?? new AgentAuthConfiguration();
        _tokenService = new Lazy<ITokenService>(serviceProvider.GetRequiredService<ITokenService>);
        _logger = serviceProvider.GetRequiredService<ILogger<JwtAgentAuthenticator>>();

        // The Lazy is only evaluated when Provider != SelfSigned.
        // If Jwt config is missing, an exception will be thrown at that point,
        // which is acceptable because external providers require configuration.
        _oidcConfigManager = new Lazy<ConfigurationManager<OpenIdConnectConfiguration>>(() =>
        {
            var jwt = _options.ExternalIdentity.Jwt ?? throw new InvalidOperationException(
                    "JWT configuration is missing. To use an external identity provider, " +
                    "configure Security:AgentAuth:Jwt.");

            var metadataUrl = !string.IsNullOrWhiteSpace(jwt.MetadataUrl)
                ? jwt.MetadataUrl
                : !string.IsNullOrWhiteSpace(jwt.Authority)
                    ? $"{jwt.Authority.TrimEnd('/')}/.well-known/openid-configuration"
                    : throw new InvalidOperationException(
                        "JWT Authority or MetadataUrl is required. To use an external identity provider, " +
                        "configure Security:AgentAuth:Jwt with a valid Authority or MetadataUrl.");

            if (!Uri.IsWellFormedUriString(metadataUrl, UriKind.Absolute))
                throw new InvalidOperationException(
                    $"The configured JWT metadata URL '{metadataUrl}' is not a valid absolute URI. " +
                    "Please check your JWT Authority or MetadataUrl configuration.");


            return new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataUrl,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever
                {
                    RequireHttps = metadataUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                });
        });
    }

    public AgentAuthResult Authenticate(Agent agent)
    {
        var token = _tokenService.Value.GenerateToken(agent);
        return AgentAuthResult.Success(token);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string credential, CancellationToken cancellationToken = default)
    {
        if (_options.ExternalIdentity.Enabled == true && _options.ExternalIdentity.Provider == ExternalIdentityProviderType.Jwt)
        {
            try
            {
                var externalPrincipal = await ValidateExternalTokenAsync(credential, cancellationToken);
                if (externalPrincipal is not null)
                    return externalPrincipal;
            }
            catch
            {
                // ignore, fall through to internal validation
            }
        }

        // Fallback to internal validation
        try
        {
            return _tokenService.Value.ValidateToken(credential);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<ClaimsPrincipal?> ValidateExternalTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (_options.ExternalIdentity.Enabled != true)
            return null;

        try
        {
            var oidcConfig = await _oidcConfigManager.Value.GetConfigurationAsync(cancellationToken);
            var jwt = _options.ExternalIdentity.Jwt!;

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate external token.");
            return null;
        }
    }
}