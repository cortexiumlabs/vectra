using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;

namespace Synentra.Infrastructure.UnitTests.Semantic;

public class ModelPackageLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ModelPackageLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static byte[] CreateZipPackageBytes(string onnxEntryName, byte[] onnxBytes)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(onnxEntryName, CompressionLevel.Optimal);
            using (var s = entry.Open())
                s.Write(onnxBytes, 0, onnxBytes.Length);

            var vocab = zip.CreateEntry("vocab.txt");
            using (var s = new StreamWriter(vocab.Open(), Encoding.UTF8))
            {
                s.WriteLine("[PAD]");
                s.WriteLine("[UNK]");
                s.WriteLine("hello");
            }

            var labels = zip.CreateEntry("labels.json");
            using (var s = new StreamWriter(labels.Open(), Encoding.UTF8))
            {
                s.Write(JsonSerializer.Serialize(new[] { "read", "write" }));
            }
        }
        return ms.ToArray();
    }

    private static byte[] CreateEncryptedBlob(byte[] plain, byte[] key)
    {
        const int NonceSize = 12;
        const int TagSize = 16;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key);
        aes.Encrypt(nonce, plain, cipher, tag);

        // Blob format expected by the loader: nonce + tag + ciphertext
        var blob = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, blob, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, blob, NonceSize + TagSize, cipher.Length);
        return blob;
    }

    [Fact]
    public void Ctor_NullDownloader_Throws()
    {
        Action act = () => new ModelPackageLoader(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Community_FileExists_ReturnsAssets_WithoutCallingDownloader()
    {
        // Arrange
        var onnx = new byte[] { 1, 2, 3 };
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEntryName, onnx);
        var path = Path.Combine(_tempDir, "community.zip");
        File.WriteAllBytes(path, bytes);

        var downloader = Substitute.For<IModelDownloader>();
        var loader = new ModelPackageLoader(downloader);
        var config = new InternalOnnxConfiguration { PackagePath = path, ModelType = "Community" };

        // Act
        var assets = await loader.LoadAsync(config, CancellationToken.None);

        // Assert
        assets.OnnxBytes.ToArray().Should().Equal(onnx);
        assets.VocabLines.Should().Contain("hello");
        assets.IntentLabels.Should().Contain("read");
        await downloader.DidNotReceiveWithAnyArgs().EnsureModelExistsAsync(default!, default);
    }

    [Fact]
    public async Task Community_FileMissing_CallsDownloader_AndSucceeds_WhenDownloaderCreatesFile()
    {
        // Arrange
        var onnx = new byte[] { 9, 8, 7 };
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEntryName, onnx);
        var path = Path.Combine(_tempDir, "community2.zip");

        var downloader = Substitute.For<IModelDownloader>();
        downloader.When(x => x.EnsureModelExistsAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(ci => File.WriteAllBytes(path, bytes));

        var loader = new ModelPackageLoader(downloader);
        var config = new InternalOnnxConfiguration { PackagePath = path, ModelType = "Community" };

        // Act
        var assets = await loader.LoadAsync(config, CancellationToken.None);

        // Assert
        assets.OnnxBytes.ToArray().Should().Equal(onnx);
        await downloader.Received(1).EnsureModelExistsAsync(Arg.Any<InternalOnnxConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Community_FileMissing_AfterDownloader_Throws_FileNotFound()
    {
        var path = Path.Combine(_tempDir, "community3.zip");
        var downloader = Substitute.For<IModelDownloader>();
        // downloader does nothing

        var loader = new ModelPackageLoader(downloader);
        var config = new InternalOnnxConfiguration { PackagePath = path, ModelType = "Community" };

        Func<Task> act = () => loader.LoadAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Pro_MissingPackagePath_Throws()
    {
        var downloader = Substitute.For<IModelDownloader>();
        var loader = new ModelPackageLoader(downloader);
        var config = new InternalOnnxConfiguration { PackagePath = null, ModelType = "Pro" };

        Func<Task> act = () => loader.LoadAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PackagePath must be configured*");
    }

    [Fact]
    public async Task Pro_LicenseFileMissing_Throws()
    {
        // Create a minimal encrypted package (content doesn't matter for this test)
        var dummy = new byte[] { 1, 2, 3, 4 };
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEncEntryName, dummy);
        var pkgPath = Path.Combine(_tempDir, "pro.zip");
        File.WriteAllBytes(pkgPath, bytes);

        var downloader = Substitute.For<IModelDownloader>();
        var loader = new ModelPackageLoader(downloader);
        var config = new InternalOnnxConfiguration { PackagePath = pkgPath, ModelType = "Pro", LicensePath = Path.Combine(_tempDir, "no-license.json") };

        Func<Task> act = () => loader.LoadAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Pro_LicenseMalformed_Throws()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var onnx = new byte[] { 11, 12 };
        var encBlob = CreateEncryptedBlob(onnx, Convert.FromBase64String(key));
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEncEntryName, encBlob);
        var pkgPath = Path.Combine(_tempDir, "pro2.zip");
        File.WriteAllBytes(pkgPath, bytes);

        var licensePath = Path.Combine(_tempDir, "license.json");
        File.WriteAllText(licensePath, "not a json");

        var loader = new ModelPackageLoader(Substitute.For<IModelDownloader>());
        var config = new InternalOnnxConfiguration { PackagePath = pkgPath, ModelType = "Pro", LicensePath = licensePath };

        Func<Task> act = () => loader.LoadAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Pro_LicenseMissingKey_Throws()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var onnx = new byte[] { 21, 22 };
        var encBlob = CreateEncryptedBlob(onnx, Convert.FromBase64String(key));
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEncEntryName, encBlob);
        var pkgPath = Path.Combine(_tempDir, "pro3.zip");
        File.WriteAllBytes(pkgPath, bytes);

        var licensePath = Path.Combine(_tempDir, "license2.json");
        File.WriteAllText(licensePath, JsonSerializer.Serialize(new { Key = "", ExpiresUtc = (DateTimeOffset?)null }));

        var loader = new ModelPackageLoader(Substitute.For<IModelDownloader>());
        var config = new InternalOnnxConfiguration { PackagePath = pkgPath, ModelType = "Pro", LicensePath = licensePath };

        Func<Task> act = () => loader.LoadAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not contain a decryption key*");
    }

    [Fact]
    public async Task Pro_LicenseExpired_Throws()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var encBlob = CreateEncryptedBlob(new byte[] { 1, 2, 3 }, keyBytes);
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEncEntryName, encBlob);
        var pkgPath = Path.Combine(_tempDir, "pro4.zip");
        File.WriteAllBytes(pkgPath, bytes);

        var license = new { Key = Convert.ToBase64String(keyBytes), ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-10) };
        var licensePath = Path.Combine(_tempDir, "license3.json");
        File.WriteAllText(licensePath, JsonSerializer.Serialize(license));

        var loader = new ModelPackageLoader(Substitute.For<IModelDownloader>());
        var config = new InternalOnnxConfiguration { PackagePath = pkgPath, ModelType = "Pro", LicensePath = licensePath };

        Func<Task> act = () => loader.LoadAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*has expired*");
    }

    [Fact]
    public async Task Pro_DecryptsSuccessfully_ReturnsPlainOnnx()
    {
        var plain = Encoding.UTF8.GetBytes("fake-onnx-bytes");
        var key = RandomNumberGenerator.GetBytes(32);
        var encBlob = CreateEncryptedBlob(plain, key);
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEncEntryName, encBlob);
        var pkgPath = Path.Combine(_tempDir, "pro5.zip");
        File.WriteAllBytes(pkgPath, bytes);

        var license = new { Key = Convert.ToBase64String(key), ExpiresUtc = (DateTimeOffset?)null };
        var licensePath = Path.Combine(_tempDir, "license4.json");
        File.WriteAllText(licensePath, JsonSerializer.Serialize(license));

        var loader = new ModelPackageLoader(Substitute.For<IModelDownloader>());
        var config = new InternalOnnxConfiguration { PackagePath = pkgPath, ModelType = "Pro", LicensePath = licensePath };

        var assets = await loader.LoadAsync(config, CancellationToken.None);

        assets.OnnxBytes.ToArray().Should().Equal(plain);
    }

    [Fact]
    public async Task Pro_EncryptedBlobTooShort_ThrowsCryptographicException()
    {
        var badBlob = new byte[5]; // too short
        var bytes = CreateZipPackageBytes(ModelPackageExtractor.OnnxEncEntryName, badBlob);
        var pkgPath = Path.Combine(_tempDir, "pro6.zip");
        File.WriteAllBytes(pkgPath, bytes);

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var license = new { Key = key, ExpiresUtc = (DateTimeOffset?)null };
        var licensePath = Path.Combine(_tempDir, "license5.json");
        File.WriteAllText(licensePath, JsonSerializer.Serialize(license));

        var loader = new ModelPackageLoader(Substitute.For<IModelDownloader>());
        var config = new InternalOnnxConfiguration { PackagePath = pkgPath, ModelType = "Pro", LicensePath = licensePath };

        Func<Task> act = () => loader.LoadAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<CryptographicException>();
    }
}
