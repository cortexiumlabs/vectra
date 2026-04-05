namespace Vectra.Infrastructure.Configuration.Logging;

public sealed record FileLoggingPolicy
{
    public string LogLevel { get; init; }
    public string? LogPath { get; init; }
    public string? RollingInterval { get; init; }
    public int? RetainedFileCountLimit { get; init; }

    public static FileLoggingPolicy Create()
    {
        return new FileLoggingPolicy
        {
            LogLevel = "Information",
            LogPath = "logs/",
            RollingInterval = "Day",
            RetainedFileCountLimit = 7
        };
    }
}