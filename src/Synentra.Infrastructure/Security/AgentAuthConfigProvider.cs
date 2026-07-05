using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

namespace Synentra.Infrastructure.Security;

public class AgentAuthConfigProvider : IAgentAuthConfigProvider
{
    private readonly AgentAuthConfiguration _config;

    public AgentAuthConfigProvider(IOptions<AgentAuthConfiguration> options)
    {
        _config = options?.Value ?? new AgentAuthConfiguration();
    }

    public TokenIssuanceConfiguration TokenIssuance => new()
    {
        Issuer = _config.TokenIssuance?.Issuer ?? string.Empty,
        Audience = _config.TokenIssuance?.Audience ?? string.Empty,
        Secret = _config.TokenIssuance?.Secret ?? string.Empty,
        Expiration = _config.TokenIssuance?.Expiration ?? TimeSpan.FromMinutes(15)
    };

    public ExternalIdentityConfiguration ExternalIdentity => new()
    {
        Provider = _config.ExternalIdentity?.Provider ?? ExternalIdentityProviderType.Jwt,
        Jwt = new JwtIdentityConfiguration
        {
            Authority = _config.ExternalIdentity?.Jwt?.Authority ?? string.Empty,
            Audience = _config.ExternalIdentity?.Jwt?.Audience ?? string.Empty,
            ValidateIssuer = _config.ExternalIdentity?.Jwt?.ValidateIssuer ?? false,
            ValidateAudience = _config.ExternalIdentity?.Jwt?.ValidateAudience ?? false
        }
    };

    public bool UseCustomHeader => _config.UseCustomHeader;
    public string CustomHeaderName => _config.CustomHeaderName ?? "Synentra-Authorization";
    public bool FallbackToAuthorization => _config.FallbackToAuthorization;
}