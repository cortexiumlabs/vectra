using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using Vectra.Application.Abstractions.Caches;
using Vectra.BuildingBlocks.Configuration.System.Storage.Cache;

namespace Vectra.Infrastructure.Caches;

public class RedisCacheProvider : ICacheProvider
{
    private readonly RedisCacheConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheProvider(
        RedisCacheConfiguration config, 
        IServiceProvider serviceProvider)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<RedisCacheProvider>>();
        _redis = _serviceProvider.GetRequiredService<IConnectionMultiplexer>();
    }

    public async Task SetAsync(string key, string value)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"hitl:{key}", JsonSerializer.Serialize(value), TimeSpan.FromMilliseconds(_config.TimeToLiveMilliseconds));
        _logger.LogInformation($"Redis ({_config.Address}) SET {key}");
    }

    public async Task<string?> GetAsync(string key)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"hitl:{key}");
        _logger.LogInformation($"Redis ({_config.Address}) GET {key}");
        return value.ToString();
    }
}