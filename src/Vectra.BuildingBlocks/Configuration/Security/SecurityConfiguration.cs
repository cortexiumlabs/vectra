using Vectra.BuildingBlocks.Configuration.Security.AgentAuth;

namespace Vectra.BuildingBlocks.Configuration.Security;

public class SecurityConfiguration
{
    public AgentAuthConfiguration AgentAuth { get; set; } = new();
}