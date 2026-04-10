namespace Vectra.BuildingBlocks.Configuration.System.Storage.Cache;

public class MemoryCacheConfiguration
{
    public double TimeToLiveMilliseconds { get; set; } = TimeSpan.FromHours(24).TotalMilliseconds;
}