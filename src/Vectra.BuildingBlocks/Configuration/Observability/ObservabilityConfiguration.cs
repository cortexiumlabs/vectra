using Vectra.BuildingBlocks.Configuration.Observability.Logging;

namespace Vectra.BuildingBlocks.Configuration.Observability;

public class ObservabilityConfiguration
{
    public LoggingConfiguration Logging { get; set; } = new();
}