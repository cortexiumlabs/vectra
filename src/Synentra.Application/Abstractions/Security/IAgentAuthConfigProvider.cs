using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

namespace Synentra.Application.Abstractions.Security;

public interface IAgentAuthConfigProvider
{
    AgentAuthProviderType Provider { get; }
    bool ValidateIssuer { get; }
    bool ValidateAudience { get; }
    string? Authority { get; }
    string? Audience { get; }
    bool UseCustomHeader { get; }
    string CustomHeaderName { get; }
    bool FallbackToAuthorization { get; }
}