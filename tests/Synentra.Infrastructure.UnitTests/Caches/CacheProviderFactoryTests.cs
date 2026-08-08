using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.BuildingBlocks.Configuration.System.Storage.Cache;
using Synentra.Infrastructure.Caches;
using Synentra.Infrastructure.Caches.Providers;

namespace Synentra.Infrastructure.UnitTests.Caches;

public class CacheProviderFactoryTests
{
    private static CacheProviderFactory CreateSut(string provider, IServiceProvider? sp = null)
    {
        var config = new SystemConfiguration();
        config.Storage.Cache.DefaultProvider = provider;
        config.Storage.Cache.Providers.Memory = new MemoryCacheConfiguration { TimeToLive = TimeSpan.FromMinutes(5) };

        if (sp == null)
        {
            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddLogging();
            sp = services.BuildServiceProvider();
        }

        return new CacheProviderFactory(Options.Create(config), sp, NullLogger<CacheProviderFactory>.Instance);
    }

    [Fact]
    public void Create_MemoryProvider_ReturnsMemoryCacheProvider()
    {
        var sut = CreateSut("memory");

        var provider = sut.Create();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void Create_UnsupportedProvider_ThrowsNotSupportedException()
    {
        var sut = CreateSut("unsupported");

        var act = () => sut.Create();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        var sp = services.BuildServiceProvider();

        var act = () => new CacheProviderFactory(null!, sp, NullLogger<CacheProviderFactory>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
    {
        var config = new SystemConfiguration();
        config.Storage.Cache.DefaultProvider = "memory";

        var act = () => new CacheProviderFactory(Options.Create(config), null!, NullLogger<CacheProviderFactory>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_RedisProvider_ReturnsRedisCacheProvider()
    {
        // Set up a service provider with a mock IConnectionMultiplexer
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NSubstitute.Substitute.For<StackExchange.Redis.IConnectionMultiplexer>());
        var sp = services.BuildServiceProvider();

        var config = new SystemConfiguration();
        config.Storage.Cache.DefaultProvider = "redis";
        config.Storage.Cache.Providers.Redis = new Synentra.BuildingBlocks.Configuration.System.Storage.Cache.RedisCacheConfiguration
        {
            Endpoint = "localhost:6379"
        };

        var sut = new CacheProviderFactory(Options.Create(config), sp, NullLogger<CacheProviderFactory>.Instance);

        var provider = sut.Create();

        provider.Should().NotBeNull();
        provider.Should().BeOfType<Synentra.Infrastructure.Caches.Providers.RedisCacheProvider>();
    }

    [Theory]
    [InlineData("MEMORY")]
    [InlineData("Memory")]
    [InlineData("MeMoRy")]
    public void Create_MemoryProvider_IsCaseInsensitive(string providerName)
    {
        var sut = CreateSut(providerName);

        var provider = sut.Create();

        provider.Should().NotBeNull();
        provider.Should().BeOfType<MemoryCacheProvider>();
    }

    [Theory]
    [InlineData(" memory")]
    [InlineData("memory ")]
    [InlineData("  memory  ")]
    public void Create_MemoryProvider_TrimWhitespace(string providerName)
    {
        var sut = CreateSut(providerName);

        var provider = sut.Create();

        provider.Should().BeOfType<MemoryCacheProvider>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RedisWithoutAddress_Throws(string? address)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<StackExchange.Redis.IConnectionMultiplexer>());

        var config = new SystemConfiguration();

        config.Storage.Cache.DefaultProvider = "redis";
        config.Storage.Cache.Providers.Redis = new RedisCacheConfiguration
        {
            Endpoint = address
        };

        var sut = new CacheProviderFactory(
            Options.Create(config),
            services.BuildServiceProvider(),
            NullLogger<CacheProviderFactory>.Instance);

        var act = () => sut.Create();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Redis is not configured*");
    }

    [Fact]
    public void Create_NullProvider_ThrowsNotSupported()
    {
        var config = new SystemConfiguration();

        config.Storage.Cache.DefaultProvider = null;

        config.Storage.Cache.Providers.Memory =
            new MemoryCacheConfiguration();

        var services = new ServiceCollection();
        services.AddMemoryCache();

        var sut = new CacheProviderFactory(
            Options.Create(config),
            services.BuildServiceProvider(),
            NullLogger<CacheProviderFactory>.Instance);

        var act = () => sut.Create();

        act.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyProvider_Throws(string provider)
    {
        var sut = CreateSut(provider);

        var act = () => sut.Create();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Constructor_MissingCacheConfiguration_Throws()
    {
        var config = new SystemConfiguration();

        config.Storage.Cache = null!;

        var services = new ServiceCollection();

        var act = () => new CacheProviderFactory(
            Options.Create(config),
            services.BuildServiceProvider(),
            NullLogger<CacheProviderFactory>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cache configuration is missing*");
    }

    [Fact]
    public void Create_RedisProvider_AddressWithWhitespace_ReturnsRedisProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(
            Substitute.For<StackExchange.Redis.IConnectionMultiplexer>());

        var config = new SystemConfiguration();

        config.Storage.Cache.DefaultProvider = "redis";

        config.Storage.Cache.Providers.Redis =
            new RedisCacheConfiguration
            {
                Endpoint = " localhost:6379 "
            };

        var sut = new CacheProviderFactory(
            Options.Create(config),
            services.BuildServiceProvider(),
            NullLogger<CacheProviderFactory>.Instance);

        var provider = sut.Create();

        provider.Should().BeOfType<RedisCacheProvider>();
    }

    [Fact]
    public void Create_UnsupportedProvider_MessageContainsProvider()
    {
        var sut = CreateSut("mongo");

        var act = () => sut.Create();

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage("*mongo*");
    }
}
