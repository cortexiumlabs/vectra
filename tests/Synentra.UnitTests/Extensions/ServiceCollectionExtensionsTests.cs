using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Abstractions.Versioning;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.Infrastructure.Persistence.Sqlite.Contexts;
using Synentra.BuildingBlocks.Clock;
using Synentra.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Synentra.UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
    // ── AddSystemClock ────────────────────────────────────────────────────

    [Fact]
    public void AddSystemClock_RegistersIClock()
    {
        var services = new ServiceCollection();
        services.AddSystemClock();

        var provider = services.BuildServiceProvider();
        var clock = provider.GetRequiredService<IClock>();

        clock.Should().NotBeNull().And.BeOfType<SystemClock>();
    }

    [Fact]
    public void AddSystemClock_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddSystemClock();
        result.Should().BeSameAs(services);
    }

    // ── AddSynentraVersion ──────────────────────────────────────────────────

    [Fact]
    public void AddSynentraVersion_RegistersIVersion()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSynentraVersion();

        var provider = services.BuildServiceProvider();
        var version = provider.GetRequiredService<IVersion>();

        version.Should().NotBeNull().And.BeOfType<Synentra.Services.SynentraVersion>();
    }

    [Fact]
    public void AddSynentraVersion_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddSynentraVersion();
        result.Should().BeSameAs(services);
    }

    // ── AddSynentraConfiguration ────────────────────────────────────────────

    [Fact]
    public void AddSynentraConfiguration_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var result = services.AddSynentraConfiguration(config);
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSynentraConfiguration_RegistersExpectedOptions()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddSynentraConfiguration(config);

        // Should not throw – confirms Options infrastructure is registered
        var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Synentra.BuildingBlocks.Configuration.System.SystemConfiguration>>();
        act.Should().NotThrow();
    }

    // ── AddSynentraHealthChecker ────────────────────────────────────────────

    [Fact]
    public void AddSynentraHealthChecker_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddSynentraHealthChecker();
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSynentraHealthChecker_RegistersHealthChecks()
    {
        var services = new ServiceCollection();
        services.AddSynentraHealthChecker();
        // Verify the health check service type is registered
        var descriptor = services.Any(s =>
            s.ServiceType == typeof(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService));
        descriptor.Should().BeTrue();
    }

    // ── AddSynentraApiDocumentation ─────────────────────────────────────────

    [Fact]
    public void AddSynentraApiDocumentation_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddSynentraApiDocumentation();
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSynentraApiDocumentation_ConfiguresSwaggerDocument()
    {
        var services = new ServiceCollection();
        services.AddSynentraApiDocumentation();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;

        options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey("synentra").Should().BeTrue();
    }

    // ── AddSynentraProxyForwarder ───────────────────────────────────────────

    [Fact]
    public void AddSynentraProxyForwarder_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddSynentraProxyForwarder();
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSynentraProxyForwarder_RegistersHttpClient()
    {
        var services = new ServiceCollection();
        services.AddSynentraProxyForwarder();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        factory.Should().NotBeNull();
    }

    // ── AddJsonSerialization (AddHttpJsonOptions alias) ───────────────────

    [Fact]
    public void AddHttpJsonOptions_ReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddHttpJsonOptions();
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddHttpJsonOptions_ConfiguresReferenceHandler()
    {
        var services = new ServiceCollection();
        services.AddHttpJsonOptions();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;

        options.SerializerOptions.ReferenceHandler.Should().Be(System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
        options.SerializerOptions.Converters.Should().Contain(converter => converter is System.Text.Json.Serialization.JsonStringEnumConverter);
    }

    [Fact]
    public void AddSynentraPersistence_SqliteProvider_RegistersPersistenceServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<SystemConfiguration>(config =>
        {
            config.Storage.Database.DefaultProvider = "sqlite";
            config.Storage.Database.Providers.Sqlite.ConnectionString = "Data Source=:memory:";
        });

        var result = services.AddSynentraPersistence();

        result.Should().BeSameAs(services);
        services.Any(s => s.ServiceType == typeof(IAgentRepository)).Should().BeTrue();
        services.Any(s => s.ServiceType == typeof(IDbContextFactory<SqliteApplicationContext>)).Should().BeTrue();
    }

    [Fact]
    public void AddSynentraPersistence_UnsupportedProvider_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<SystemConfiguration>(config =>
        {
            config.Storage.Database.DefaultProvider = "unknown";
        });

        var act = () => services.AddSynentraPersistence();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Unsupported database provider*");
    }
}
