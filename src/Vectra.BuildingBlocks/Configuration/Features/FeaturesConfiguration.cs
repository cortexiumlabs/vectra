using Vectra.BuildingBlocks.Configuration.Features.Hitl;

namespace Vectra.BuildingBlocks.Configuration.Features;

public class FeaturesConfiguration
{
    public HitlConfiguration Hitl { get; set; } = new();
}