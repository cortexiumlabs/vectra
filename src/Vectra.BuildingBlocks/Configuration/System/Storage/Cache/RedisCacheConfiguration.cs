namespace Vectra.BuildingBlocks.Configuration.System.Storage.Cache;

public class RedisCacheConfiguration
{
    public string Address { get; set; } = default!;
    public double TimeToLiveMilliseconds { get; set; } = TimeSpan.FromHours(24).TotalMilliseconds;
}