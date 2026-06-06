namespace Vectra.BuildingBlocks.Configuration.Observability.Logging;

public class OtlpLoggingConfiguration
{
    public bool Enabled { get; set; } = false;
    public string? Endpoint { get; set; }
    public string LogLevel { get; set; } = "Information";
}
