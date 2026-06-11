namespace Synentra.BuildingBlocks.Configuration.Observability.Logging;

public class OtlpLoggingConfiguration
{
    public bool Enabled { get; set; } = false;
    public string? Endpoint { get; set; }
    public string LogLevel { get; set; } = "Information";

    public static OtlpLoggingConfiguration Create()
    {
        return new OtlpLoggingConfiguration
        {
            LogLevel = "Information"
        };
    }
}
