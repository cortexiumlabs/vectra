using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Caches;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.BuildingBlocks.Configuration.System.Storage.Cache;
using Synentra.Infrastructure.Caches.Providers;

namespace Synentra.Infrastructure.Caches;

public sealed class CacheProviderFactory : ICacheProviderFactory
{
    private readonly CacheConfiguration _config;
    private readonly IServiceProvider _serviceProvider;

    public CacheProviderFactory(IOptions<SystemConfiguration> options, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _config = options.Value.Storage.Cache
            ?? throw new InvalidOperationException("Cache configuration is missing.");

        _serviceProvider = serviceProvider;
    }

    public ICacheProvider Create()
    {
        var provider = _config.DefaultProvider?.Trim();

        return provider?.ToLowerInvariant() switch
        {
            "memory" => CreateMemory(),

            "redis" when !string.IsNullOrWhiteSpace(
                _config.Providers.Redis.Address) =>
                CreateRedis(),

            "redis" =>
                throw new InvalidOperationException(
                    "Redis cache provider is selected but Redis is not configured."),

            _ =>
                throw new NotSupportedException(
                    $"Cache provider '{_config.DefaultProvider}' is not supported.")
        };
    }

    private ICacheProvider CreateRedis() =>
        ActivatorUtilities.CreateInstance<RedisCacheProvider>(_serviceProvider, _config.Providers.Redis);

    private ICacheProvider CreateMemory() =>
        ActivatorUtilities.CreateInstance<MemoryCacheProvider>(_serviceProvider, _config.Providers.Memory);
}