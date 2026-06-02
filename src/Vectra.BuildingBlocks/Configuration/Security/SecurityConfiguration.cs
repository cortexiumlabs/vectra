using Vectra.BuildingBlocks.Configuration.Security.AgentAuth;
using Vectra.BuildingBlocks.Configuration.Security.AgentQuarantine;

namespace Vectra.BuildingBlocks.Configuration.Security;

public class SecurityConfiguration
{
    public AgentAuthConfiguration AgentAuth { get; set; } = new();
    public AgentQuarantineConfiguration AgentQuarantine { get; set; } = new();
}