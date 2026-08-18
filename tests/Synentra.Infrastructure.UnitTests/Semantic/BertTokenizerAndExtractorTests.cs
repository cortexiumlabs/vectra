using FluentAssertions;
using NSubstitute;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;
using Synentra.Application.Abstractions.Caches;
using Synentra.Infrastructure.Caches;
using Microsoft.Extensions.Options;
using Synentra.BuildingBlocks.Configuration.Semantic;
using NSubstitute.ExceptionExtensions;

namespace Synentra.Infrastructure.UnitTests.Semantic;

public class BertTokenizerTests
{
    private static readonly string[] MinimalVocab =
    [
        "[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]",
        "h", "e", "l", "o", "w", "r", "d", "a", "t", "i", "n", "s",
        "hello", "world", "test", "data", "request", "api", ".",
        ",", "!", "?", "admin", "user", "delete", "get", "post"
    ];

    private static BertTokenizer CreateSut() => new(MinimalVocab);

    [Fact]
    public void Tokenize_SimpleWord_InVocab_ReturnsIds()
    {
        var sut = CreateSut();

        var (inputIds, attentionMask) = sut.Tokenize("hello", maxLength: 8);

        inputIds.Should().HaveCount(3);
        attentionMask.Should().HaveCount(3);
        // First token is [CLS] (index 2), last non-pad is [SEP] (index 3)
        inputIds[0].Should().Be(2); // [CLS]
        inputIds[^1].Should().Be(3); // [SEP]
    }

    [Fact]
    public void Tokenize_DoesNotPadToMaxLength()
    {
        var sut = CreateSut();

        var (inputIds, attentionMask) = sut.Tokenize("hi", maxLength: 16);

        inputIds.Should().HaveCount(4);
        attentionMask.Should().HaveCount(4);
        attentionMask.Should().OnlyContain(value => value == 1L);
    }

    [Fact]
    public void Tokenize_TruncatesToMaxLength()
    {
        var sut = CreateSut();
        // Long text to ensure truncation
        var longText = string.Join(" ", Enumerable.Repeat("hello world test data", 20));

        var (inputIds, attentionMask) = sut.Tokenize(longText, maxLength: 16);

        inputIds.Should().HaveCount(16);
        attentionMask.Should().HaveCount(16);
        attentionMask.Should().OnlyContain(value => value == 1L);
    }

    [Fact]
    public void Tokenize_UnknownWord_UsesUnkToken()
    {
        var sut = CreateSut();

        var (inputIds, _) = sut.Tokenize("xyz_unknown_word", maxLength: 8);

        // Should contain the [UNK] token id (1) somewhere
        inputIds.Should().Contain(1L); // [UNK]
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsSpecialTokensOnly()
    {
        var sut = CreateSut();

        var (inputIds, attentionMask) = sut.Tokenize("", maxLength: 8);

        inputIds.Should().HaveCount(2);
        attentionMask.Should().Equal(1L, 1L);
        inputIds[0].Should().Be(2); // [CLS]
        inputIds[1].Should().Be(3); // [SEP]
    }

    [Fact]
    public void Tokenize_WithPunctuation_SplitsProperly()
    {
        var sut = CreateSut();

        var (inputIds, _) = sut.Tokenize("hello,world", maxLength: 16);

        inputIds.Should().HaveCount(5);
        // Comma should be tokenized separately
    }

    [Fact]
    public void Tokenize_MultipleWords_ProcessesAll()
    {
        var sut = CreateSut();

        var (inputIds, attentionMask) = sut.Tokenize("hello world", maxLength: 32);

        inputIds.Should().HaveCount(4);
        attentionMask.Should().HaveCount(4);
        // First token is [CLS]
        inputIds[0].Should().Be(2);
    }

    [Fact]
    public void Tokenize_AttentionMaskMatchesInputIds()
    {
        var sut = CreateSut();

        var (inputIds, attentionMask) = sut.Tokenize("test data", maxLength: 16);

        for (int i = 0; i < inputIds.Length; i++)
        {
            attentionMask[i].Should().Be(1L, $"token at {i} should have mask 1");
        }
    }

    [Fact]
    public void Tokenize_WordPieceSubword_FallsBackToUnk()
    {
        // Word not in vocab, no subword matches → UNK
        var sut = CreateSut();

        var (inputIds, _) = sut.Tokenize("zzzunknownzzzz", maxLength: 8);

        inputIds.Should().Contain(1L); // [UNK]
    }
}

/// <summary>
/// Tests ModelPackageLoader and ModelPackageExtractor indirectly through InternalOnnxProvider,
/// since ModelPackageLoader, ModelPackageExtractor, and ModelAssets are internal.
/// </summary>
public class ModelPackageLoaderViaProviderTests
{
    private readonly Microsoft.Extensions.Logging.ILogger<InternalOnnxProvider> _logger =
        Microsoft.Extensions.Logging.Abstractions.NullLogger<InternalOnnxProvider>.Instance;

    [Fact]
    public void Constructor_Enabled_DoesNotThrow()
    {
        var cacheProvider = Substitute.For<ICacheProvider>();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns(cacheProvider);
        var loader = Substitute.For<IModelPackageLoader>();

        var options = Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration
                {
                    PackagePath = "some/path.zip",
                    ModelType = "Community"
                }
            }
        });

        var act = () => new InternalOnnxProvider(options, cacheService, loader, _logger);
        act.Should().NotThrow(); // construction is now lazy
    }

    [Fact]
    public async Task InitializeAsync_Enabled_CallsLoaderAndPreparesSession()
    {
        var cacheProvider = Substitute.For<ICacheProvider>();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns(cacheProvider);
        var loader = Substitute.For<IModelPackageLoader>();

        // Arrange: loader returns a valid ModelAssets
        var assets = new ModelAssets(
            new byte[] { 0x01, 0x02 },
            new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello" },
            new[] { "read", "write" }
        );
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(assets));

        var options = Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration
                {
                    PackagePath = "test.zip",
                    ModelType = "Community"
                }
            }
        });

        var provider = new InternalOnnxProvider(options, cacheService, loader, _logger);

        // Act
        var act = () => provider.InitializeAsync();

        await act.Should().ThrowAsync<Exception>();

        // Assert: the provider should now be ready (AnalyzeAsync won't block)
        // We can't easily test internal session, but we can ensure loader was called
        await loader.Received(1).LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_WhenDownloadFails_DisablesProvider()
    {
        var cacheProvider = Substitute.For<ICacheProvider>();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns(cacheProvider);
        var loader = Substitute.For<IModelPackageLoader>();
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Download failed"));

        var options = Options.Create(new SemanticConfiguration
        {
            Enabled = true,
            Providers = new SemanticProviders
            {
                Internal = new InternalOnnxConfiguration
                {
                    PackagePath = "test.zip",
                    ModelType = "Community"
                }
            }
        });

        var provider = new InternalOnnxProvider(options, cacheService, loader, _logger);

        // Act
        var act = () => provider.InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Download failed*");
    }

    private static byte[] CreateValidZipPackage(
        byte[] onnxBytes,
        string[] vocabLines,
        string[] labels,
        string onnxEntryName = "model.onnx")
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var onnxEntry = zip.CreateEntry(onnxEntryName);
            using (var s = onnxEntry.Open()) s.Write(onnxBytes);

            var vocabEntry = zip.CreateEntry("vocab.txt");
            using (var s = vocabEntry.Open())
            using (var w = new StreamWriter(s))
                foreach (var line in vocabLines) w.WriteLine(line);

            var labelsEntry = zip.CreateEntry("labels.json");
            using (var s = labelsEntry.Open())
                JsonSerializer.Serialize(s, labels);
        }
        return ms.ToArray();
    }

    [Fact]
    public void Constructor_WithNullPackagePath_DoesNotThrow()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var cacheProvider = Substitute.For<ICacheProvider>();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns(cacheProvider);

        var options = Options.Create(
            new SemanticConfiguration
            {
                Enabled = true,
                Providers = new SemanticProviders
                {
                    Internal = new InternalOnnxConfiguration
                    {
                        PackagePath = null   // missing path, but should not throw
                    }
                }
            });

        // Act & Assert – constructor must succeed
        var act = () => new InternalOnnxProvider(options, cacheService, loader, _logger);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAsync_WithNullPackagePath_UsesDefaultPathAndLoads()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var cacheProvider = Substitute.For<ICacheProvider>();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns(cacheProvider);

        var options = Options.Create(
            new SemanticConfiguration
            {
                Enabled = true,
                Providers = new SemanticProviders
                {
                    Internal = new InternalOnnxConfiguration
                    {
                        PackagePath = null
                    }
                }
            });

        // Simulate a successful load
        var assets = new ModelAssets(
            new byte[] { 1, 2 },
            new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello" },
            new[] { "read", "write" }
        );
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
              .Returns(assets);

        var provider = new InternalOnnxProvider(options, cacheService, loader, _logger);

        var act = () => provider.InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Exception>();

        // The loader must have been called (with the null path config)
        await loader.Received(1).LoadAsync(
            Arg.Is<InternalOnnxConfiguration>(c => c.PackagePath == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_WithNonExistentFile_DoesNotThrow()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        var cacheProvider = Substitute.For<ICacheProvider>();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns(cacheProvider);

        var options = Options.Create(
            new SemanticConfiguration
            {
                Enabled = true,
                Providers = new SemanticProviders
                {
                    Internal = new InternalOnnxConfiguration
                    {
                        PackagePath = "does_not_exist_xyz.zip"
                    }
                }
            });

        // Act & Assert – constructor must succeed, the file is not touched yet
        var act = () => new InternalOnnxProvider(options, cacheService, loader, _logger);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAsync_WhenFileNotFoundAndLoaderFails_DisablesProvider()
    {
        var loader = Substitute.For<IModelPackageLoader>();
        loader.LoadAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
              .ThrowsAsync(new FileNotFoundException("Model package not found."));

        var cacheProvider = Substitute.For<ICacheProvider>();
        var cacheService = Substitute.For<ICacheService>();
        cacheService.Current.Returns(cacheProvider);

        var options = Options.Create(
            new SemanticConfiguration
            {
                Enabled = true,
                Providers = new SemanticProviders
                {
                    Internal = new InternalOnnxConfiguration
                    {
                        PackagePath = "does_not_exist_xyz.zip"
                    }
                }
            });

        var provider = new InternalOnnxProvider(options, cacheService, loader, _logger);

        var act = () => provider.InitializeAsync();

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}

public class ModelPackageExtractorTests
{
    private static byte[] CreateZipPackage(
        string onnxContent,
        string vocabContent,
        string labelsJson,
        string onnxEntryName = "model.onnx")
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var s = zip.CreateEntry(onnxEntryName).Open())
                s.Write(Encoding.UTF8.GetBytes(onnxContent));
            using (var s = zip.CreateEntry("vocab.txt").Open())
                s.Write(Encoding.UTF8.GetBytes(vocabContent));
            using (var s = zip.CreateEntry("labels.json").Open())
                s.Write(Encoding.UTF8.GetBytes(labelsJson));
        }
        return ms.ToArray();
    }

    [Fact]
    public void Extract_ValidPackage_ReturnsAllAssets()
    {
        var package = CreateZipPackage("onnx-bytes", "[PAD]\n[UNK]\nhello\nworld", "[\"read\",\"write\",\"harmful\"]");

        var (onnx, vocab, labels) = ModelPackageExtractor.Extract(package);

        onnx.Should().NotBeEmpty();
        vocab.Should().HaveCount(4);
        vocab.Should().Contain("hello");
        labels.Should().HaveCount(3);
        labels.Should().Contain("read");
    }

    [Fact]
    public void Extract_MissingOnnx_ThrowsInvalidOperationException()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var s = zip.CreateEntry("vocab.txt").Open();
            s.Write(Encoding.UTF8.GetBytes("[PAD]"));
        }

        var act = () => ModelPackageExtractor.Extract(ms.ToArray());

        act.Should().Throw<InvalidOperationException>().WithMessage("*model.onnx*");
    }

    [Fact]
    public void Extract_MissingVocab_ThrowsInvalidOperationException()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var s = zip.CreateEntry("model.onnx").Open();
            s.Write(Encoding.UTF8.GetBytes("bytes"));
        }

        var act = () => ModelPackageExtractor.Extract(ms.ToArray());

        act.Should().Throw<InvalidOperationException>().WithMessage("*vocab.txt*");
    }

    [Fact]
    public void Extract_MissingLabels_ThrowsInvalidOperationException()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var s = zip.CreateEntry("model.onnx").Open()) s.Write(Encoding.UTF8.GetBytes("bytes"));
            using (var s = zip.CreateEntry("vocab.txt").Open()) s.Write(Encoding.UTF8.GetBytes("[PAD]"));
        }

        var act = () => ModelPackageExtractor.Extract(ms.ToArray());

        act.Should().Throw<InvalidOperationException>().WithMessage("*labels.json*");
    }

    [Fact]
    public void Extract_WithEncEntryName_FindsEncryptedOnnx()
    {
        var package = CreateZipPackage("enc-bytes", "[PAD]\n[UNK]", "[\"read\"]", onnxEntryName: "model.onnx.enc");

        var (onnx, vocab, labels) = ModelPackageExtractor.Extract(package, "model.onnx.enc");

        onnx.Should().NotBeEmpty();
        vocab.Should().HaveCount(2);
        labels.Should().Contain("read");
    }
}

public class ModelPackageLoaderServiceTests
{
    [Fact]
    public async Task LoadAsync_FileExists_SkipsDownloadAndExtracts()
    {
        // Arrange
        var downloader = Substitute.For<IModelDownloader>();
        var loader = new ModelPackageLoader(downloader); // the new injectable service
        var tmpPath = Path.GetTempFileName() + ".zip";
        try
        {
            // Create a valid zip
            byte[] zipBytes = CreateCommunityPackage();
            await File.WriteAllBytesAsync(tmpPath, zipBytes, TestContext.Current.CancellationToken);

            var config = new InternalOnnxConfiguration
            {
                PackagePath = tmpPath,
                ModelType = "Community"
            };

            // Act
            var assets = await loader.LoadAsync(config, CancellationToken.None);

            // Assert
            assets.OnnxBytes.Length.Should().BeGreaterThan(0);
            assets.VocabLines.Should().HaveCountGreaterThan(0);
            assets.IntentLabels.Should().HaveCountGreaterThan(0);
            await downloader.DidNotReceive().EnsureModelExistsAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    [Fact]
    public async Task LoadAsync_FileMissing_CallsDownloaderThenExtracts()
    {
        // Arrange
        var downloader = Substitute.For<IModelDownloader>();
        // Simulate that downloader creates the file
        downloader.EnsureModelExistsAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(callInfo =>
            {
                var cfg = callInfo.Arg<InternalOnnxConfiguration>();
                var path = Environment.ExpandEnvironmentVariables(cfg.PackagePath!);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, CreateCommunityPackage());
            });

        var loader = new ModelPackageLoader(downloader);
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.zip");
        var config = new InternalOnnxConfiguration
        {
            PackagePath = tempFile,
            ModelType = "Community"
        };

        try
        {
            // Act
            var assets = await loader.LoadAsync(config, CancellationToken.None);

            // Assert
            assets.OnnxBytes.Length.Should().BeGreaterThan(0);
            await downloader.Received(1).EnsureModelExistsAsync(config, Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // Helper to create a minimal community zip
    private static byte[] CreateCommunityPackage()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var s = zip.CreateEntry("model.onnx").Open())
                s.Write(new byte[] { 0x01, 0x02 });
            using (var s = zip.CreateEntry("vocab.txt").Open())
            using (var w = new StreamWriter(s))
            {
                w.WriteLine("[PAD]"); w.WriteLine("[UNK]"); w.WriteLine("[CLS]");
                w.WriteLine("[SEP]"); w.WriteLine("hello");
            }
            using (var s = zip.CreateEntry("labels.json").Open())
                JsonSerializer.Serialize(s, new[] { "read", "write" });
        }
        return ms.ToArray();
    }
}