using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Extensions.Logging;
using StackExchange.Redis;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Executions;
using Vectra.Application.Abstractions.Security;
using Vectra.Application.Abstractions.Serializations;
using Vectra.BuildingBlocks.Configuration.Features;
using Vectra.BuildingBlocks.Configuration.System;
using Vectra.Infrastructure.Caches;
using Vectra.Infrastructure.Decision;
using Vectra.Infrastructure.Dispatchers;
using Vectra.Infrastructure.Hitl;
using Vectra.Infrastructure.Policy;
using Vectra.Infrastructure.Risk;
using Vectra.Infrastructure.Security;
using Vectra.Infrastructure.Semantic;
using Vectra.Infrastructure.Serializations.Json;

namespace Vectra.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, JwtTokenService>();

        // Register the selected authenticator scheme
        services.AddScoped<IAgentAuthenticator, JwtAgentAuthenticator>();
        services.AddSingleton<ISecretHasher, BcryptSecretHasher>();

        // Policy engine
        services.AddSingleton<IPolicyLoader, FileSystemPolicyLoader>();
        services.AddScoped<IPolicyEngine, PolicyEngine>();

        // Risk scoring
        services.AddScoped<IRiskScoringService, RiskScoringService>();

        // Semantic engine (stub)
        services.AddScoped<ISemanticEngine, SemanticEngineStub>();

        services.AddMemoryCache();

        // HITL provider selection (DI + factory method)
        services.AddDistributedMemoryCache();

        services.AddScoped<IHitlService>(CreateHitlService);

        // Decision engine
        services.AddScoped<IDecisionEngine, DecisionEngine>();
        services.AddScoped<IDispatcher, Dispatcher>();

        // YARP forwarder
        services.AddHttpForwarder();

        return services;
    }

    private static IHitlService CreateHitlService(IServiceProvider sp)
    {
        var features = sp.GetRequiredService<IOptions<FeaturesConfiguration>>().Value;
        var provider = features.Hitl?.Provider;

        return string.Equals(provider, "Redis", StringComparison.OrdinalIgnoreCase)
            ? ActivatorUtilities.CreateInstance<RedisHitlService>(sp)
            : ActivatorUtilities.CreateInstance<InMemoryHitlService>(sp);
    }

    public static IServiceCollection AddVectraLogging(this IServiceCollection services)
    {
        services.AddSingleton<Logging.ILoggerFactory, Logging.LoggerFactory>();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.Services.AddSingleton<ILoggerProvider>(sp =>
            {
                var logger = sp.GetRequiredService<Logging.ILoggerFactory>().CreateLogger();
                Log.Logger = logger;
                return new SerilogLoggerProvider(logger, dispose: true);
            });
        });

        return services;
    }

    public static IServiceCollection AddCache(this IServiceCollection services)
    {
        services.AddSingleton<ICacheProviderFactory, CacheProviderFactory>();
        services.AddSingleton<ICacheService, CacheService>();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<SystemConfiguration>>().Value;
            var redisConfig = config.Storage.Cache.Redis;

            var options = ConfigurationOptions.Parse(redisConfig.Address);
            options.AbortOnConnectFail = redisConfig.AbortOnConnectFail ?? false;
            options.ConnectRetry = redisConfig.ConnectRetry ?? 5;
            options.ConnectTimeout = redisConfig.ConnectTimeout ?? 5000;
            options.ReconnectRetryPolicy = new ExponentialRetry(redisConfig.ConnectTimeout ?? 5000);
            return ConnectionMultiplexer.Connect(options);
        });

        return services;
    }

    public static IServiceCollection AddJsonSerialization(this IServiceCollection services)
    {
        services
            .AddSingleton<ISerializer, JsonSerializer>()
            .AddSingleton<IDeserializer, JsonDeserializer>();

        return services;
    }
}