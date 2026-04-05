namespace Vectra.Infrastructure.Configuration.Logging;

public sealed record SeqLoggingPolicy
{
    public string LogLevel { get; init; }
    public string? ApiKey { get; init; }
    public string? Url { get; init; }

    public static SeqLoggingPolicy Create()
    {
        return new SeqLoggingPolicy
        {
            LogLevel = "Information"
        };
    }
}