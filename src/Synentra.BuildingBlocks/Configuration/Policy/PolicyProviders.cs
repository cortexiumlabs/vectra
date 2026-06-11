namespace Synentra.BuildingBlocks.Configuration.Policy;

public class PolicyProviders
{
    public InternalPolicyConfiguration Internal { get; set; } = new();
    public OpaPolicyConfiguration Opa { get; set; } = new();
}