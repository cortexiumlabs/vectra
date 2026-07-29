using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Caches;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Infrastructure.Caches;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;

namespace Synentra.Infrastructure.UnitTests.Semantic;

public class InternalOnnxInitializerTests
{
    // --------------------------------------------------
    // Tests
    // --------------------------------------------------
    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNothing()
    {
        var config = new SemanticConfiguration { Enabled = false };
        var serviceProvider = new FakeServiceProvider(shouldThrowOnResolve: true);
        var logger = new FakeLogger<InternalOnnxInitializer>();
        var sut = CreateSut(config, serviceProvider, logger);

        await sut.StartAsync(CancellationToken.None);

        serviceProvider.ResolveCalls.Should().BeEmpty();
        logger.InformationLogs.Should().BeEmpty();
    }

    [Theory]
    [InlineData("external")]
    [InlineData("azure")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsync_WhenProviderIsNotInternal_DoesNothing(string providerName)
    {
        var config = new SemanticConfiguration
        {
            Enabled = true,
            DefaultProvider = providerName
        };
        var serviceProvider = new FakeServiceProvider(shouldThrowOnResolve: true);
        var logger = new FakeLogger<InternalOnnxInitializer>();
        var sut = CreateSut(config, serviceProvider, logger);

        await sut.StartAsync(CancellationToken.None);

        serviceProvider.ResolveCalls.Should().BeEmpty();
        logger.InformationLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_WhenDefaultProviderIsNull_ResolvesProvider()
    {
        var config = new SemanticConfiguration
        {
            Enabled = true,
            DefaultProvider = null
        };
        var serviceProvider = new FakeServiceProvider();
        serviceProvider.AddService(typeof(InternalOnnxProvider), CreateDummyInternalOnnxProvider());
        var logger = new FakeLogger<InternalOnnxInitializer>();
        var sut = CreateSut(config, serviceProvider, logger);

        await sut.StartAsync(CancellationToken.None);

        serviceProvider.ResolveCalls.Should().ContainSingle()
            .Which.Should().Be(typeof(InternalOnnxProvider));
        logger.InformationLogs.Should().BeEmpty();   // no logging in this version
    }

    [Theory]
    [InlineData("internal")]
    [InlineData("Internal")]
    [InlineData("INTERNAL")]
    [InlineData("  internal  ")]
    public async Task StartAsync_WhenProviderIsInternal_ResolvesProvider(string providerName)
    {
        var config = new SemanticConfiguration
        {
            Enabled = true,
            DefaultProvider = providerName
        };
        var serviceProvider = new FakeServiceProvider();
        serviceProvider.AddService(typeof(InternalOnnxProvider), CreateDummyInternalOnnxProvider());
        var logger = new FakeLogger<InternalOnnxInitializer>();
        var sut = CreateSut(config, serviceProvider, logger);

        await sut.StartAsync(CancellationToken.None);

        serviceProvider.ResolveCalls.Should().ContainSingle()
            .Which.Should().Be(typeof(InternalOnnxProvider));
        logger.InformationLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task StopAsync_ReturnsCompletedTask()
    {
        var sut = CreateSut(
            new SemanticConfiguration(),
            new FakeServiceProvider(),
            new FakeLogger<InternalOnnxInitializer>());

        var task = sut.StopAsync(CancellationToken.None);

        task.Should().Be(Task.CompletedTask);
        await task; // no exception
    }

    // --------------------------------------------------
    // Factory & helpers
    // --------------------------------------------------
    private static InternalOnnxInitializer CreateSut(
        SemanticConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<InternalOnnxInitializer> logger)
    {
        var options = Options.Create(config);
        return new InternalOnnxInitializer(serviceProvider, options, logger);
    }

    private static InternalOnnxProvider CreateDummyInternalOnnxProvider()
    {
        // Use Enabled = false so the constructor doesn't try to load models
        var options = Options.Create(new SemanticConfiguration { Enabled = false });
        var cacheService = new DummyCacheService();
        var logger = new FakeLogger<InternalOnnxProvider>();
        return new InternalOnnxProvider(options, cacheService, logger);
    }

    // --------------------------------------------------
    // Test doubles
    // --------------------------------------------------
    private class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly bool _shouldThrowOnResolve;
        public List<Type> ResolveCalls { get; } = new();

        public FakeServiceProvider(bool shouldThrowOnResolve = false)
            => _shouldThrowOnResolve = shouldThrowOnResolve;

        public void AddService(Type type, object instance)
            => _services[type] = instance;

        public object? GetService(Type serviceType)
        {
            ResolveCalls.Add(serviceType);
            if (_shouldThrowOnResolve)
                throw new InvalidOperationException("Unexpected service resolution");

            return _services.TryGetValue(serviceType, out var instance) ? instance : null;
        }
    }

    private class FakeLogger<T> : ILogger<T>
    {
        public List<string> InformationLogs { get; } = new();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
                InformationLogs.Add(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable BeginScope<TState>(TState state) => null!;
    }

    private class DummyCacheService : ICacheService
    {
        public ICacheProvider Current => new DummyCacheProvider();
    }

    private class DummyCacheProvider : ICacheProvider
    {
        public Task<object?> GetAsync(object key) => throw new NotImplementedException();
        public Task<TItem?> GetAsync<TItem>(object key) => throw new NotImplementedException();
        public Task RemoveAsync(object key) => throw new NotImplementedException();
        public Task<TItem> SetAsync<TItem>(object key, TItem value) => throw new NotImplementedException();
        public Task<(bool success, TItem? value)> TryGetValueAsync<TItem>(string key) => throw new NotImplementedException();
    }
}

public class ProviderSection
{
    public InternalProviderConfig Internal { get; set; } = new InternalProviderConfig();
}

public class InternalProviderConfig
{
    public int? MaxLength { get; set; }
    // Other properties omitted for brevity – not used with Enabled=false
}