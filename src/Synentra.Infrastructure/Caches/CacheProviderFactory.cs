using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<CacheProviderFactory> _logger;

    public CacheProviderFactory(
        IOptions<SystemConfiguration> options, 
        IServiceProvider serviceProvider,
        ILogger<CacheProviderFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _config = options.Value.Storage.Cache
            ?? throw new InvalidOperationException("Cache configuration is missing.");

        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ICacheProvider Create()
    {
        var configuredProvider = _config.DefaultProvider?.Trim();

        var provider = configuredProvider?.ToLowerInvariant() switch
        {
            "memory" => CreateMemory(),

            "redis" when !string.IsNullOrWhiteSpace(
                _config.Providers.Redis.Endpoint) =>
                CreateRedis(),

            "redis" =>
                throw new InvalidOperationException(
                    "Redis cache provider is selected but Redis is not configured."),

            _ =>
                throw new NotSupportedException(
                    $"Cache provider '{_config.DefaultProvider}' is not supported.")
        };

        _logger.LogInformation(
            "Cache provider selected and created. Provider={ConfiguredProvider}",
            configuredProvider);

        return provider;
    }

    private ICacheProvider CreateRedis()
    {
        _logger.LogInformation(
            "Initializing Redis cache provider. Endpoint={RedisEndpoint}",
            _config.Providers.Redis.Endpoint);

        return ActivatorUtilities.CreateInstance<RedisCacheProvider>(
            _serviceProvider,
            _config.Providers.Redis);
    }

    private ICacheProvider CreateMemory()
    {
        _logger.LogInformation(
            "Initializing 'Memory' cache provider.");

        return ActivatorUtilities.CreateInstance<MemoryCacheProvider>(
            _serviceProvider,
            _config.Providers.Memory);
    }
}