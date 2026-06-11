namespace Vectra.BuildingBlocks.Configuration.System.RateLimit;

public class RateLimitConfiguration
{
    public bool Enabled { get; set; } = true;
    public int DefaultRequestsPerMinute { get; set; } = 60;
}
