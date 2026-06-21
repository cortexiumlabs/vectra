namespace Synentra.BuildingBlocks.Configuration.System.Storage.Cache;

public class CacheConfiguration
{
    public string DefaultProvider { get; set; } = "Memory";
    public CacheProviders Providers { get; set; } = new();
}