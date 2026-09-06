using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Synentra.Application.Abstractions.Versioning;
using Synentra.BuildingBlocks.Clock;
using Synentra.BuildingBlocks.Configuration.HumanInTheLoop;
using Synentra.BuildingBlocks.Configuration.Observability;
using Synentra.BuildingBlocks.Configuration.Policy;
using Synentra.BuildingBlocks.Configuration.Risk;
using Synentra.BuildingBlocks.Configuration.SecretManagement;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.BuildingBlocks.Configuration.System.Cors;
using Synentra.Infrastructure.Persistence.Sqlite;
using Synentra.Services;
using System.Text.Json.Serialization;
using static OllamaSharp.OllamaApiClient;

namespace Synentra.Extensions;

public static class ServiceCollectionExtensions
{
    private const string SystemConfigurationName = "System";
    private const string ObservabilityConfigurationName = "Observability";
    private const string SecurityConfigurationName = "Security";
    private const string SemanticConfigurationName = "Semantic";
    private const string HumanInTheLoopConfigurationName = "HumanInTheLoop";
    private const string PolicyConfigurationName = "Policy";
    private const string SecretManagementConfigurationName = "SecretManagement";
    private const string RiskConfigurationName = "Risk";

    #region Simple registrations

    public static IServiceCollection AddSystemClock(this IServiceCollection services)
    {
        services.AddScoped<IClock, SystemClock>();
        return services;
    }
    public static IServiceCollection AddSynentraVersion(this IServiceCollection services)
    {
        services.AddSingleton<IVersion, SynentraVersion>();
        return services;
    }

    public static IServiceCollection AddSynentraConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SystemConfiguration>(configuration.GetSection(SystemConfigurationName));
        services.Configure<ObservabilityConfiguration>(configuration.GetSection(ObservabilityConfigurationName));
        services.Configure<SecurityConfiguration>(configuration.GetSection(SecurityConfigurationName));
        services.Configure<SemanticConfiguration>(configuration.GetSection(SemanticConfigurationName));
        services.Configure<HumanInTheLoopConfiguration>(configuration.GetSection(HumanInTheLoopConfigurationName));
        services.Configure<PolicyConfiguration>(configuration.GetSection(PolicyConfigurationName));
        services.Configure<SecretManagementConfiguration>(configuration.GetSection(SecretManagementConfigurationName));
        services.Configure<RiskConfiguration>(configuration.GetSection(RiskConfigurationName));

        return services;
    }

    #endregion

    #region Health checks

    public static IServiceCollection AddSynentraHealthChecker(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }

    #endregion

    #region OpenAPI (Swagger)

    public static IServiceCollection AddSynentraApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("synentra", new OpenApiInfo
            {
                Version = "synentra",
                Title = "Service Invocation",
                Description = "Using the service invocation API to find out how to communicate with Synentra API.",
                License = new OpenApiLicense
                {
                    Name = "Apache License Version 2.0",
                    Url = new Uri("https://www.apache.org/licenses/")
                }
            });
        });

        return services;
    }

    #endregion

    #region JSON options

    public static IServiceCollection AddHttpJsonOptions(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });

        return services;
    }

    #endregion

    #region Persistence

    public static IServiceCollection AddSynentraPersistence(this IServiceCollection services)
    {
        using var scope = services.BuildServiceProvider().CreateScope();
        var systemConfig = scope.ServiceProvider.GetRequiredService<IOptions<SystemConfiguration>>().Value;

        var provider = systemConfig.Storage.Database.DefaultProvider;

        switch (provider?.ToLowerInvariant())
        {
            case "sqlite":
                services.AddSqlitePersistenceLayer();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported database provider '{provider}'.");
        }

        return services;
    }

    #endregion

    #region HttpClient

    public static IServiceCollection AddSynentraProxyForwarder(this IServiceCollection services)
    {
        services.AddHttpClient("ProxyForwarder")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.ConnectionClose = false;
            });
        return services;
    }

    #endregion

    #region Cors

    public static IServiceCollection AddSynentraCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("SynentraCors", policy =>
            {
                using var scope = services.BuildServiceProvider().CreateScope();
                var systemConfig = scope.ServiceProvider.GetRequiredService<IOptions<SystemConfiguration>>().Value;

                var corsConfiguration = systemConfig.Cors;

                policy.WithOrigins(corsConfiguration.AllowedOrigins);

                if (corsConfiguration.AllowedMethods.Contains("*", StringComparer.Ordinal))
                    policy.AllowAnyMethod();
                else
                    policy.WithMethods(corsConfiguration.AllowedMethods);

                if (corsConfiguration.AllowedHeaders.Contains("*", StringComparer.Ordinal))
                    policy.AllowAnyHeader();
                else
                    policy.WithHeaders(corsConfiguration.AllowedHeaders);
            });
        });

        return services;
    }

    #endregion
}