using Vectra.BuildingBlocks.Configuration.Observability.Logging;
using Vectra.BuildingBlocks.Configuration.Observability.OpenTelemetry;

namespace Vectra.BuildingBlocks.Configuration.Observability;

public class ObservabilityConfiguration
{
    public LoggingConfiguration Logging { get; set; } = new();
    public OpenTelemetryConfiguration? OpenTelemetry { get; set; } = new();
}
