namespace Synentra.BuildingBlocks.Configuration.Risk;

public sealed class RiskConfiguration
{
    public bool? Enabled { get; set; } = true;
    public RiskWeightsConfiguration Weights { get; set; } = new();
}
