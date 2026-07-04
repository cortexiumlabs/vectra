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

    public AgentAuthProviderType Provider => _config.Provider;
    public bool ValidateIssuer => _config.Jwt?.ValidateIssuer ?? false;
    public bool ValidateAudience => _config.Jwt?.ValidateAudience ?? false;
    public string? Authority => _config.Jwt?.Authority;
    public string? Audience => _config.Jwt?.Audience;
    public bool UseCustomHeader => _config.UseCustomHeader;
    public string CustomHeaderName => _config.CustomHeaderName ?? "Synentra-Authorization";
    public bool FallbackToAuthorization => _config.FallbackToAuthorization;
}