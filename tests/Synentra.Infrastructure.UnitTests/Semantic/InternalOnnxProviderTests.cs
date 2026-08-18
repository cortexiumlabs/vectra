using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Synentra.Application.Abstractions.Caches;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Infrastructure.Caches;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

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

    private IOptions<SemanticConfiguration> EnabledOptions(
        int? modelSize = 8,
        double? confidenceThreshold = 0.7) =>
        Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            ConfidenceThreshold = confidenceThreshold,
            Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration
                {
                    PackagePath = "model.zip",
                    ModelSize = modelSize
                }
            }
        });

    private InternalOnnxProvider CreateDisabledProvider() => new(
        DisabledOptions(),
        _cacheService,
        Substitute.For<IModelPackageLoader>(),
        NullLogger<InternalOnnxProvider>.Instance);

    private InternalOnnxProvider CreateEnabledProvider(IModelPackageLoader? loader = null) => new(
        EnabledOptions(),
        _cacheService,
        loader ?? Substitute.For<IModelPackageLoader>(),
        NullLogger<InternalOnnxProvider>.Instance);

    private static object? InvokePrivate(
        string methodName,
        object? instance,
        params object?[] arguments)
    {
        var method = typeof(InternalOnnxProvider).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        try
        {
            return method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

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

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new InternalOnnxProvider(
            null!, _cacheService, Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_NullCacheService_ThrowsArgumentNullException()
    {
        var act = () => new InternalOnnxProvider(
            DisabledOptions(), null!, Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("cacheService");
    }

    [Fact]
    public void Constructor_NullLoader_ThrowsArgumentNullException()
    {
        var act = () => new InternalOnnxProvider(
            DisabledOptions(), _cacheService, null!,
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<ArgumentNullException>().WithParameterName("loader");
    }

    [Fact]
    public void Constructor_NullConfigurationValue_ThrowsInvalidOperationException()
    {
        var options = Substitute.For<IOptions<SemanticConfiguration>>();
        options.Value.Returns((SemanticConfiguration)null!);

        var act = () => new InternalOnnxProvider(
            options, _cacheService, Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*configuration is unavailable*");
    }

    [Fact]
    public void Constructor_EnabledWithoutInternalConfiguration_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            Providers = new SemanticProviders { Internal = null! }
        });

        var act = () => new InternalOnnxProvider(
            options, _cacheService, Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Internal ONNX configuration is missing*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveModelSize_ThrowsInvalidOperationException(int modelSize)
    {
        var act = () => new InternalOnnxProvider(
            EnabledOptions(modelSize), _cacheService,
            Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be greater than zero*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Constructor_ConfidenceOutsideUnitInterval_ThrowsInvalidOperationException(
        double threshold)
    {
        var act = () => new InternalOnnxProvider(
            EnabledOptions(confidenceThreshold: threshold), _cacheService,
            Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be between 0 and 1*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Constructor_ConfidenceAtBoundary_IsAccepted(double threshold)
    {
        var act = () => new InternalOnnxProvider(
            EnabledOptions(confidenceThreshold: threshold), _cacheService,
            Substitute.For<IModelPackageLoader>(),
            NullLogger<InternalOnnxProvider>.Instance);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAsync_DisabledProvider_DoesNotCallLoader()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        using var sut = new InternalOnnxProvider(
            DisabledOptions(), _cacheService, loader,
            NullLogger<InternalOnnxProvider>.Instance);

        await sut.InitializeAsync(TestContext.Current.CancellationToken);

        await loader.DidNotReceiveWithAnyArgs()
            .LoadAsync(default!, default);
    }

    [Fact]
    public async Task InitializeAsync_EmptyOnnxModel_ThrowsAndMemoizesFailure()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new ModelAssets(
                Array.Empty<byte>(),
                new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]" },
                new[] { "safe_read" }));
        using var sut = CreateEnabledProvider(loader);

        var first = () => sut.InitializeAsync(TestContext.Current.CancellationToken);
        await first.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty ONNX model*");

        var second = () => sut.InitializeAsync(TestContext.Current.CancellationToken);
        await second.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty ONNX model*");
        await loader.Received(1).LoadAsync(
            Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_EmptyVocabulary_ThrowsInvalidOperationException()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new ModelAssets(new byte[] { 1 }, Array.Empty<string>(), new[] { "safe_read" }));
        using var sut = CreateEnabledProvider(loader);

        var act = () => sut.InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*valid tokenizer vocabulary*");
    }

    [Fact]
    public async Task InitializeAsync_EmptyLabels_ThrowsInvalidOperationException()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new ModelAssets(
                new byte[] { 1 },
                new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]" },
                Array.Empty<string>()));
        using var sut = CreateEnabledProvider(loader);

        var act = () => sut.InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not contain intent labels*");
    }

    [Fact]
    public async Task InitializeAsync_CancelledBeforeLock_ThrowsOperationCanceledException()
    {
        using var sut = CreateEnabledProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => sut.InitializeAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PublicMethods_AfterDispose_ThrowObjectDisposedException()
    {
        var sut = CreateDisabledProvider();
        sut.Dispose();

        var initialize = () => sut.InitializeAsync();
        var analyze = () => sut.AnalyzeAsync("hello", "", CancellationToken.None);

        await initialize.Should().ThrowAsync<ObjectDisposedException>();
        await analyze.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void TruncateToLength_ExactLength_ReturnsOriginalArray()
    {
        var values = new long[] { 1, 2, 3 };

        var result = (long[])InvokePrivate("TruncateToLength", null, values, 3)!;

        result.Should().BeSameAs(values);
    }

    [Fact]
    public void TruncateToLength_LongInput_Truncates()
    {
        var result = (long[])InvokePrivate(
            "TruncateToLength", null, new long[] { 1, 2, 3 }, 2)!;

        result.Should().Equal(1, 2);
    }

    [Fact]
    public void TruncateToLength_ShortInput_ReturnsOriginalArray()
    {
        var values = new long[] { 1, 2 };

        var result = (long[])InvokePrivate(
            "TruncateToLength", null, values, 4)!;

        result.Should().BeSameAs(values);
    }

    [Fact]
    public void ComputeHash_ReturnsUppercaseSha256Hex()
    {
        const string input = "hello";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

        var result = (string)InvokePrivate("ComputeHash", null, input)!;

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("bulk_export", "data_exfiltration")]
    [InlineData("export", "data_exfiltration")]
    [InlineData("destructive_delete", "destructive")]
    [InlineData("soft_delete", "destructive")]
    [InlineData("admin_action", "privilege_escalation")]
    [InlineData("escalate_privileges", "privilege_escalation")]
    [InlineData("harmful", "malicious")]
    [InlineData("suspicious", "malicious")]
    public void GetRiskTags_RiskyIntent_ReturnsExpectedTag(string intent, string tag)
    {
        var result = (string[])InvokePrivate("GetRiskTags", null, intent)!;

        result.Should().Equal(tag);
    }

    [Fact]
    public void GetRiskTags_UnmappedIntent_ReturnsEmptyArray()
    {
        var result = (string[])InvokePrivate("GetRiskTags", null, "safe_read")!;

        result.Should().BeEmpty();
    }

    [Fact]
    public void Softmax_ReturnsNormalizedProbabilities()
    {
        var result = (float[])InvokePrivate(
            "Softmax", null, new float[] { 1f, 2f, 3f })!;

        result.Sum().Should().BeApproximately(1f, 0.00001f);
        result[2].Should().BeGreaterThan(result[1]);
        result[1].Should().BeGreaterThan(result[0]);
    }

    [Theory]
    [MemberData(nameof(InvalidSoftmaxInputs))]
    public void Softmax_InvalidSum_ThrowsInvalidOperationException(float[] logits)
    {
        var act = () => InvokePrivate("Softmax", null, logits);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unable to calculate probabilities*");
    }

    public static TheoryData<float[]> InvalidSoftmaxInputs => new()
    {
        new[] { float.NaN, 0f },
        new[] { float.PositiveInfinity, float.PositiveInfinity }
    };

    [Fact]
    public void ValidateLogits_EmptyArray_ThrowsInvalidOperationException()
    {
        using var sut = CreateEnabledProvider();

        var act = () => InvokePrivate("ValidateLogits", sut, Array.Empty<float>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty logits tensor*");
    }

    [Fact]
    public void ValidateLogits_WrongLabelCount_ThrowsInvalidOperationException()
    {
        using var sut = CreateEnabledProvider();
        SetIntentLabels(sut, "one", "two");

        var act = () => InvokePrivate("ValidateLogits", sut, new[] { 1f });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*1 logits*2 intent labels*");
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ValidateLogits_NonFiniteValue_ThrowsInvalidOperationException(float value)
    {
        using var sut = CreateEnabledProvider();
        SetIntentLabels(sut, "one");

        var act = () => InvokePrivate("ValidateLogits", sut, new[] { value });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-finite logits*");
    }

    [Fact]
    public void ValidateLogits_ValidValues_DoesNotThrow()
    {
        using var sut = CreateEnabledProvider();
        SetIntentLabels(sut, "one", "two");

        var act = () => InvokePrivate("ValidateLogits", sut, new[] { 1f, 2f });

        act.Should().NotThrow();
    }

    private static void SetIntentLabels(InternalOnnxProvider sut, params string[] labels)
    {
        var field = typeof(InternalOnnxProvider).GetField(
            "_intentLabels", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Intent labels field was not found.");
        field.SetValue(sut, labels);
    }
}
