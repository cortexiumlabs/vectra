namespace Synentra.BuildingBlocks.Configuration.System.Storage.Cache;

public class CacheConfiguration
{
    public string DefaultProvider { get; set; } = "Redis";
    public CacheProviders Providers { get; set; } = new();
}