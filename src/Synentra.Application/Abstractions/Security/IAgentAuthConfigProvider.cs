using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

namespace Synentra.Application.Abstractions.Security;

public interface IAgentAuthConfigProvider
{
    TokenIssuanceConfiguration TokenIssuance { get; }
    ExternalIdentityConfiguration ExternalIdentity { get; }
    bool UseCustomHeader { get; }
    string CustomHeaderName { get; }
    bool FallbackToAuthorization { get; }
}