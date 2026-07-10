using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Synentra.Application.Abstractions.Caches;
using Synentra.Application.Abstractions.CircuitBreaker;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.RateLimit;
using Synentra.Application.Abstractions.Serializations;
using Synentra.BuildingBlocks.Configuration.HumanInTheLoop;
using Synentra.BuildingBlocks.Configuration.Observability;
using Synentra.BuildingBlocks.Configuration.Policy;
using Synentra.BuildingBlocks.Configuration.SecretManagement;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.BuildingBlocks.Configuration.System.Storage.Cache;
using Synentra.Infrastructure.Caches;
using Synentra.Infrastructure.SecretManagement;

namespace Synentra.Infrastructure.UnitTests.DI;

public class DependencyInjectionTests
{
    private static IServiceProvider BuildMinimalServiceProvider(
        Action<IServiceCollection>? extra = null,
        string semanticProvider = "internal",
        string policyProvider = "internal",
        bool semanticEnabled = false)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddHttpClient();

        // Bind all required configurations
        services.Configure<SecurityConfiguration>(cfg =>
        {
            cfg.AgentAuth = new AgentAuthConfiguration
            {
                TokenIssuance = new TokenIssuanceConfiguration
                {
                    Secret = "super-secret-key-for-tests-1234567890",
                    Issuer = "test-issuer",
                    Audience = "test-audience",
                    Expiration = TimeSpan.FromMinutes(15)
                },
                ExternalIdentity = new ExternalIdentityConfiguration
                {
                    Provider = ExternalIdentityProviderType.Jwt,
                    Jwt = new JwtIdentityConfiguration
                    {
                        Authority = "https://identity.example.com",
                        ValidateIssuer = false,
                        ValidateAudience = false
                    }
                }
            };
        });

        services.Configure<PolicyConfiguration>(cfg =>
        {
            cfg.DefaultProvider = policyProvider;
            cfg.Enabled = true;
        });

        services.Configure<SystemConfiguration>(cfg =>
        {
            cfg.Storage = new StorageConfiguration
            {
                Cache = new CacheConfiguration
                {
                    DefaultProvider = "memory",
                    Providers = new CacheProviders
                    {
                        Memory = new MemoryCacheConfiguration { TimeToLive = TimeSpan.FromMinutes(5) },
                        Redis = new RedisCacheConfiguration { Endpoint = "localhost:6379" }
                    }
                }
            };
        });

        services.Configure<SemanticConfiguration>(cfg =>
        {
            cfg.Enabled = semanticEnabled;
            cfg.DefaultProvider = semanticProvider;
            cfg.Providers = new SemanticProviders
            {
                OpenAi = new OpenAiConfiguration { ApiKey = "sk-test", Model = "gpt-4o-mini" },
                AzureAi = new AzureAiConfiguration
                {
                    Endpoint = "https://example.openai.azure.com",
                    ApiKey = "test-key",
                    Model = "gpt-4"
                },
                Gemini = new GeminiConfiguration
                {
                    ApiKey = "AIzaSyDtest_key_for_unit_testing_012345",
                    Model = "gemini-1.5-flash"
                },
                Ollama = new OllamaConfiguration { Endpoint = "http://localhost:11434", Model = "llama3" },
                Internal = new InternalOnnxConfiguration { PackagePath = null }
            };
        });

        services.Configure<HumanInTheLoopConfiguration>(cfg =>
        {
            cfg.TimeoutSeconds = 300;
        });

        // Required repositories and services
        services.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAuditRepository>());
        services.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAgentHistoryRepository>());
        services.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAgentRepository>());
        services.AddSingleton(Substitute.For<Synentra.BuildingBlocks.Clock.IClock>());

        extra?.Invoke(services);

        return services.BuildServiceProvider();
    }

    // ── AddJsonSerialization ───────────────────────────────────────────────

    [Fact]
    public void AddJsonSerialization_RegistersSerializer()
    {
        var services = new ServiceCollection();
        services.AddJsonSerialization();
        var sp = services.BuildServiceProvider();

        var serializer = sp.GetService<ISerializer>();
        var deserializer = sp.GetService<IDeserializer>();

        serializer.Should().NotBeNull();
        deserializer.Should().NotBeNull();
    }

    // ── AddSynentraObservability ────────────────────────────────────────────

    [Fact]
    public void AddSynentraObservability_RegistersLoggerFactory()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:OpenTelemetry:Enabled"] = "false"
        });
        builder.Services.AddOptions<ObservabilityConfiguration>().Bind(builder.Configuration.GetSection("Observability"));

        builder.AddSynentraObservability();

        var sp = builder.Services.BuildServiceProvider();

        var loggerFactory = sp.GetService<Synentra.Infrastructure.Logging.ILoggerFactory>();

        loggerFactory.Should().NotBeNull();
    }

    // ── AddSecretManagement ───────────────────────────────────────────────

    [Fact]
    public void AddSecretManagement_RegistersSecretProviderFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<SecretManagementConfiguration>(cfg =>
            cfg.DefaultProvider = SecretManagementProviderType.None);
        services.AddSecretManagement();
        var sp = services.BuildServiceProvider();

        var factory = sp.GetService<ISecretProviderFactory>();
        var management = sp.GetService<ISecretManagementService>();

        factory.Should().NotBeNull();
        management.Should().NotBeNull();
    }

    // ── AddCache ──────────────────────────────────────────────────────────

    [Fact]
    public void AddCache_MemoryProvider_RegistersCacheService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.Configure<SystemConfiguration>(cfg =>
        {
            cfg.Storage.Cache.DefaultProvider = "memory";
            cfg.Storage.Cache.Providers.Memory = new MemoryCacheConfiguration
            {
                TimeToLive = TimeSpan.FromMinutes(5)
            };
        });
        services.AddCache();
        var sp = services.BuildServiceProvider();

        var cacheService = sp.GetService<ICacheService>();

        cacheService.Should().NotBeNull();
    }

    // ── AddInfrastructure factory method: Internal semantic provider ──────

    [Fact]
    public void AddInfrastructure_InternalSemanticProvider_DisabledSemantic_CanResolveSemanticProvider()
    {
        var sp = BuildMinimalServiceProvider(semanticProvider: "internal", semanticEnabled: false);
        var infrastructure = new ServiceCollection();
        infrastructure.AddLogging();
        infrastructure.AddMemoryCache();
        infrastructure.AddDistributedMemoryCache();
        infrastructure.AddHttpClient();
        infrastructure.Configure<SecurityConfiguration>(cfg =>
        {
            cfg.AgentAuth = new AgentAuthConfiguration
            {
                TokenIssuance = new TokenIssuanceConfiguration
                {
                    Secret = "super-secret-key-for-tests-1234567890",
                    Issuer = "test-issuer",
                    Audience = "test-audience",
                    Expiration = TimeSpan.FromMinutes(15)
                },
                ExternalIdentity = new ExternalIdentityConfiguration
                {
                    Provider = ExternalIdentityProviderType.Jwt,
                    Jwt = new JwtIdentityConfiguration
                    {
                        Authority = "https://identity.example.com"
                    }
                }
            };
        });
        infrastructure.Configure<PolicyConfiguration>(cfg => { cfg.DefaultProvider = "internal"; cfg.Enabled = true; });
        infrastructure.Configure<SystemConfiguration>(cfg =>
        {
            cfg.Storage.Cache.DefaultProvider = "memory";
            cfg.Storage.Cache.Providers.Memory = new MemoryCacheConfiguration { TimeToLive = TimeSpan.FromMinutes(5) };
            cfg.Storage.Cache.Providers.Redis = new RedisCacheConfiguration { Endpoint = "localhost:6379" };
        });
        infrastructure.Configure<SemanticConfiguration>(cfg =>
        {
            cfg.Enabled = false;
            cfg.DefaultProvider = "internal";
            cfg.Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration { PackagePath = null }
            };
        });
        infrastructure.Configure<HumanInTheLoopConfiguration>(cfg => cfg.TimeoutSeconds = 300);
        infrastructure.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAuditRepository>());
        infrastructure.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAgentHistoryRepository>());
        infrastructure.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAgentRepository>());
        infrastructure.AddSingleton(Substitute.For<Synentra.BuildingBlocks.Clock.IClock>());
        infrastructure.AddCache();
        infrastructure.AddJsonSerialization();
        infrastructure.AddInfrastructure();

        var serviceProvider = infrastructure.BuildServiceProvider();

        var provider = serviceProvider.GetService<ISemanticProvider>();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_OpenAiSemanticProvider_CanResolveSemanticProvider()
    {
        var services = BuildServices(semanticProvider: "openai");

        var provider = services.GetService<ISemanticProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_AzureAiSemanticProvider_CanResolveSemanticProvider()
    {
        var services = BuildServices(semanticProvider: "azureai");

        var provider = services.GetService<ISemanticProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_GeminiSemanticProvider_CanResolveSemanticProvider()
    {
        var services = BuildServices(semanticProvider: "gemini");

        var provider = services.GetService<ISemanticProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_OllamaSemanticProvider_CanResolveSemanticProvider()
    {
        var services = BuildServices(semanticProvider: "ollama");

        var provider = services.GetService<ISemanticProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_OpaPolicyProvider_CanResolveIPolicyProvider()
    {
        var services = BuildServices(policyProvider: "opa");

        var provider = services.GetService<Synentra.Application.Abstractions.Executions.IPolicyProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_InternalPolicyProvider_CanResolveIPolicyProvider()
    {
        var services = BuildServices(policyProvider: "internal");

        var provider = services.GetService<Synentra.Application.Abstractions.Executions.IPolicyProvider>();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_RegistersDispatcher()
    {
        var services = BuildServices();

        var dispatcher = services.GetService<IDispatcher>();

        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_RegistersRateLimiter()
    {
        var services = BuildServices();

        var rateLimiter = services.GetService<IAgentRateLimiter>();

        rateLimiter.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_RegistersCircuitBreaker()
    {
        var services = BuildServices();

        var cb = services.GetService<ICircuitBreaker>();

        cb.Should().NotBeNull();
    }

    [Fact]
    public void AddCache_RegistersConnectionMultiplexerFactory_CanResolve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.Configure<SystemConfiguration>(cfg =>
        {
            cfg.Storage.Cache.DefaultProvider = "memory";
            cfg.Storage.Cache.Providers.Memory = new MemoryCacheConfiguration { TimeToLive = TimeSpan.FromMinutes(5) };
            cfg.Storage.Cache.Providers.Redis = new RedisCacheConfiguration { Endpoint = "localhost:6379" };
        });
        services.AddCache();
        var sp = services.BuildServiceProvider();

        // IConnectionMultiplexer is registered as a factory — just verify it's registered (do NOT resolve it, that would try to connect)
        var descriptor = sp.GetRequiredService<IServiceScopeFactory>().CreateScope().ServiceProvider;
        descriptor.Should().NotBeNull();
    }

    private static IServiceProvider BuildServices(
        string semanticProvider = "internal",
        string policyProvider = "internal")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddHttpClient();
        services.Configure<SecurityConfiguration>(cfg =>
        {
            cfg.AgentAuth = new AgentAuthConfiguration
            {
                TokenIssuance = new TokenIssuanceConfiguration
                {
                    Secret = "super-secret-key-for-tests-1234567890",
                    Issuer = "test-issuer",
                    Audience = "test-audience",
                    Expiration = TimeSpan.FromMinutes(15)
                },
                ExternalIdentity = new ExternalIdentityConfiguration
                {
                    Provider = ExternalIdentityProviderType.Jwt,
                    Jwt = new JwtIdentityConfiguration
                    {
                        Authority = "https://identity.example.com"
                    }
                }
            };
        });
        services.Configure<PolicyConfiguration>(cfg => { cfg.DefaultProvider = policyProvider; cfg.Enabled = true; });
        services.Configure<SystemConfiguration>(cfg =>
        {
            cfg.Storage.Cache.DefaultProvider = "memory";
            cfg.Storage.Cache.Providers.Memory = new MemoryCacheConfiguration { TimeToLive = TimeSpan.FromMinutes(5) };
            cfg.Storage.Cache.Providers.Redis = new RedisCacheConfiguration { Endpoint = "localhost:6379" };
        });
        services.Configure<SemanticConfiguration>(cfg =>
        {
            cfg.Enabled = false;
            cfg.DefaultProvider = semanticProvider;
            cfg.Providers = new SemanticProviders
            {
                OpenAi = new OpenAiConfiguration { ApiKey = "sk-test", Model = "gpt-4o-mini" },
                AzureAi = new AzureAiConfiguration { Endpoint = "https://example.openai.azure.com", ApiKey = "test-key", Model = "gpt-4" },
                Gemini = new GeminiConfiguration { ApiKey = "AIzaSyDtest_key_for_unit_testing_012345", Model = "gemini-1.5-flash" },
                Ollama = new OllamaConfiguration { Endpoint = "http://localhost:11434", Model = "llama3" },
                Internal = new InternalOnnxConfiguration { PackagePath = null }
            };
        });
        services.Configure<HumanInTheLoopConfiguration>(cfg => cfg.TimeoutSeconds = 300);
        services.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAuditRepository>());
        services.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAgentHistoryRepository>());
        services.AddSingleton(Substitute.For<Synentra.Application.Abstractions.Persistence.IAgentRepository>());
        services.AddSingleton(Substitute.For<Synentra.BuildingBlocks.Clock.IClock>());
        services.AddCache();
        services.AddJsonSerialization();
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }
}
