using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Synentra.Application.Abstractions.Caches;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Infrastructure.Caches;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;

namespace Synentra.Infrastructure.UnitTests.Semantic;

public class InternalOnnxProviderTests
{
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ICacheProvider _cacheProvider = Substitute.For<ICacheProvider>();

    public InternalOnnxProviderTests()
    {
        _cacheService.Current.Returns(_cacheProvider);
    }

    private static IOptions<SemanticConfiguration> DisabledOptions() =>
        Options.Create(new SemanticConfiguration { Enabled = false });

    [Fact]
    public void Constructor_DisabledSemantic_DoesNotLoadModel()
    {
        // Should not throw even with no PackagePath configured
        var loader = Substitute.For<IModelPackageLoader>();
        var act = () => new InternalOnnxProvider(
            DisabledOptions(), _cacheService, loader, NullLogger<InternalOnnxProvider>.Instance);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AnalyzeAsync_DisabledProvider_ReturnsFallback()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var sut = new InternalOnnxProvider(
            DisabledOptions(), _cacheService, loader, NullLogger<InternalOnnxProvider>.Instance);

        var result = await sut.AnalyzeAsync("request body", "/api", CancellationToken.None);

        result.Intent.Should().Be("suspicious");
        result.Confidence.Should().Be(0.5);
        result.FallbackSafe.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_DisabledProvider_NullBody_ReturnsFallback()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var sut = new InternalOnnxProvider(
            DisabledOptions(), _cacheService, loader, NullLogger<InternalOnnxProvider>.Instance);

        var result = await sut.AnalyzeAsync(null, "/api", CancellationToken.None);

        result.Intent.Should().Be("suspicious");
        result.FallbackSafe.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_DisabledProvider_WhitespaceBody_ReturnsFallback()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var sut = new InternalOnnxProvider(
            DisabledOptions(), _cacheService, loader, NullLogger<InternalOnnxProvider>.Instance);

        var result = await sut.AnalyzeAsync("   ", "/api", CancellationToken.None);

        result.Intent.Should().Be("suspicious");
        result.FallbackSafe.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DisabledProvider_DoesNotThrow()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var sut = new InternalOnnxProvider(
            DisabledOptions(), _cacheService, loader, NullLogger<InternalOnnxProvider>.Instance);

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAsync_WithNullPackagePath_UsesDefaultPathAndLoadsModel()
    {
        // Arrange
        var loader = Substitute.For<IModelPackageLoader>();
        var assets = new ModelAssets(
            new byte[] { 1, 2 },
            new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello" },
            new[] { "read", "write" }
        );
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
              .Returns(assets);

        var options = Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration
                {
                    PackagePath = null,
                    ModelSize = 64
                }
            }
        });

        var provider = new InternalOnnxProvider(
            options, _cacheService, loader, NullLogger<InternalOnnxProvider>.Instance);

        // Act
        var act = () => provider.InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Exception>();

        // Assert – the loader was called with the same configuration (including the null PackagePath)
        await loader.Received(1).LoadAsync(Arg.Is<InternalOnnxConfiguration>(c => c.PackagePath == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_WhenLoaderFails_ProviderIsDisabledAndReturnsFallbackSafe()
    {
        // Arrange
        var loader = Substitute.For<IModelPackageLoader>();
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new FileNotFoundException("Model package not found."));

        var options = Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration
                {
                    PackagePath = "nonexistent_model_package.zip",
                    ModelSize = 64
                }
            }
        });

        var provider = new InternalOnnxProvider(
            options, _cacheService, loader, NullLogger<InternalOnnxProvider>.Instance);

        // Act
        var act = () => provider.InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new InternalOnnxProvider(
            DisabledOptions(),
            _cacheService,
            Substitute.For<IModelPackageLoader>(),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_EnabledWithoutCacheProvider_ThrowsArgumentNullException()
    {
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns((ICacheProvider?)null);

        var options = Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration
                {
                    PackagePath = "dummy.zip"
                }
            }
        });

        var act = () => new InternalOnnxProvider(
            options,
            cacheService,
            Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No cache provider is currently configured*");
    }

    [Fact]
    public async Task AnalyzeAsync_WhitespaceBody_DoesNotAccessCache()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var sut = new InternalOnnxProvider(
            DisabledOptions(),
            _cacheService,
            loader,
            NullLogger<InternalOnnxProvider>.Instance);

        await sut.AnalyzeAsync("   ", "", CancellationToken.None);

        await _cacheProvider
            .DidNotReceiveWithAnyArgs()
            .TryGetValueAsync<object>(default!);
    }

    [Fact]
    public async Task AnalyzeAsync_NullBody_DoesNotAccessCache()
    {
        var sut = new InternalOnnxProvider(
            DisabledOptions(),
            _cacheService,
            Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        await sut.AnalyzeAsync(null, "", CancellationToken.None);

        await _cacheProvider
            .DidNotReceiveWithAnyArgs()
            .TryGetValueAsync<object>(default!);
    }

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var sut = new InternalOnnxProvider(
            DisabledOptions(),
            _cacheService,
            loader,
            NullLogger<InternalOnnxProvider>.Instance);

        sut.Dispose();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AnalyzeAsync_DisabledProvider_IgnoresMetadata()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var sut = new InternalOnnxProvider(
            DisabledOptions(),
            _cacheService,
            loader,
            NullLogger<InternalOnnxProvider>.Instance);

        var result = await sut.AnalyzeAsync(
            "hello",
            Guid.NewGuid().ToString(),
            CancellationToken.None);

        result.Intent.Should().Be("suspicious");
    }
}
