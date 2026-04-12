namespace Vectra.BuildingBlocks.Configuration.Features.Policy;

public class PolicyConfiguration
{
    public bool? Enabled { get; set; } = true;
    public string Provider { get; set; } = "Internal";
    public InternalPolicyConfiguration Internal { get; set; } = new();
    public OpaPolicyConfiguration Opa { get; set; } = new();
}