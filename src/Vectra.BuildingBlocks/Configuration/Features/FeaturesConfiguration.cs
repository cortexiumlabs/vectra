using Vectra.BuildingBlocks.Configuration.Features.Hitl;
using Vectra.BuildingBlocks.Configuration.Features.Policy;
using Vectra.BuildingBlocks.Configuration.Features.Semantic;

namespace Vectra.BuildingBlocks.Configuration.Features;

public class FeaturesConfiguration
{
    public HitlConfiguration Hitl { get; set; } = new();
    public PolicyConfiguration Policy { get; set; } = new();
    public SemanticConfiguration Semantic { get; set; } = new();
}