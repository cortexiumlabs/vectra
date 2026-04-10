namespace Vectra.BuildingBlocks.Configuration.System.Storage.Cache;

public class CacheConfiguration
{
    public string Provider { get; set; } = "Redis";
    public RedisCacheConfiguration Redis { get; set; } = new();
    public MemoryCacheConfiguration Memory { get; set; } = new();
}