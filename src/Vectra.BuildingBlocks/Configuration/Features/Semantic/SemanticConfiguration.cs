namespace Vectra.BuildingBlocks.Configuration.Features.Semantic;

public class SemanticConfiguration
{
    public bool Enabled { get; set; } = false;
    public double ConfidenceThreshold { get; set; } = 0.7;
}
