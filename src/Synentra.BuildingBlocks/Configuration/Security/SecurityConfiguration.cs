using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.BuildingBlocks.Configuration.Security.AgentQuarantine;

namespace Synentra.BuildingBlocks.Configuration.Security;

public class SecurityConfiguration
{
    public AgentAuthConfiguration AgentAuth { get; set; } = new();
    public AgentQuarantineConfiguration AgentQuarantine { get; set; } = new();
}