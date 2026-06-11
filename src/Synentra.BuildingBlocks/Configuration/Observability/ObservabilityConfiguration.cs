using Synentra.BuildingBlocks.Configuration.Observability.Logging;
using Synentra.BuildingBlocks.Configuration.Observability.OpenTelemetry;

namespace Synentra.BuildingBlocks.Configuration.Observability;

public class ObservabilityConfiguration
{
    public LoggingConfiguration Logging { get; set; } = new();
    public OpenTelemetryConfiguration? OpenTelemetry { get; set; } = new();
}
