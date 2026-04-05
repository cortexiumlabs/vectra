namespace Vectra.Infrastructure.Configuration.Logging;

public sealed record LoggingPolicy
{
    public bool Enabled { get; init; } = false;
    public string DefaultLogLevel { get; init; } = "Information";
    public FileLoggingPolicy File { get; init; } = new();
    public SeqLoggingPolicy Seq { get; set; } = new();

    public static LoggingPolicy Create()
    {
        return new LoggingPolicy
        {
            Enabled = false,
            File = FileLoggingPolicy.Create(),
            Seq = SeqLoggingPolicy.Create()
        };
    }
}