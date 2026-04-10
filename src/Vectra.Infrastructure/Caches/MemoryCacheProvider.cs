using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vectra.Application.Abstractions.Caches;
using Vectra.BuildingBlocks.Configuration.System.Storage.Cache;

namespace Vectra.Infrastructure.Caches;

public class MemoryCacheProvider : ICacheProvider
{
    private readonly MemoryCacheConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheProvider> _logger;

    public MemoryCacheProvider(
        MemoryCacheConfiguration config, 
        IServiceProvider serviceProvider)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        _logger = _serviceProvider.GetRequiredService<ILogger<MemoryCacheProvider>>();
    }

    public Task SetAsync(string key, string value)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(_config.TimeToLiveMilliseconds)
        };

        _cache.Set(key, value, options);
        _logger.LogInformation($"InMemory SET {key}");
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        _cache.TryGetValue(key, out string? value);
        _logger.LogInformation($"InMemory GET {key}");
        return Task.FromResult(value);
    }
}