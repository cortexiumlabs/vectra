using FluentAssertions;
using Microsoft.Extensions.Logging;
using Octokit;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;
using System.Security.Cryptography;

namespace Synentra.Infrastructure.UnitTests.Semantic;

public class ModelDownloaderTests : IDisposable
{
    private readonly string _tempDir;

    public ModelDownloaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // --- Helper to create a minimal zip file (for temp file) ---
    private string CreateTempFile(string fileName, byte[] content)
    {
        string path = Path.Combine(_tempDir, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    // --- Tests ---

    [Fact]
    public async Task FileAlreadyExists_ReturnsWithoutDownload()
    {
        // Arrange
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var httpFactory = new FakeHttpClientFactory();
        var downloader = new ModelDownloader(logger, gitHub, httpFactory);

        string existingFile = CreateTempFile("model.zip", new byte[] { 1, 2, 3 });
        var config = new InternalOnnxConfiguration { PackagePath = existingFile, ModelType = "Community" };

        // Act
        await downloader.EnsureModelExistsAsync(config, CancellationToken.None);

        // Assert – GitHub client was never called
        logger.LogMessages.Should().Contain(msg => msg.Contains("Model already exists at"));
        // No other interactions
    }

    [Fact]
    public async Task FileDoesNotExist_DownloadsModel()
    {
        // Arrange
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var httpFactory = new FakeHttpClientFactory();

        // Prepare a fake release with one asset (no checksum)
        var release = CreateFakeRelease(assetName: "intent-model-community.zip", assetUrl: "http://example.com/model.zip");
        gitHub.OnGetLatestRelease = (o, r) => Task.FromResult(release);

        // Set up HttpClient to return the model content
        httpFactory.AddHandler("ModelDownloader", () => new FakeHttpMessageHandler(async (request, ct) =>
        {
            // Return dummy model content
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 5, 6, 7 })
            };
        }));

        var downloader = new ModelDownloader(logger, gitHub, httpFactory);
        string targetPath = Path.Combine(_tempDir, "model-output.zip");
        var config = new InternalOnnxConfiguration { PackagePath = targetPath, ModelType = "Community" };

        // Act
        await downloader.EnsureModelExistsAsync(config, CancellationToken.None);

        // Assert
        File.Exists(targetPath).Should().BeTrue();
        logger.LogMessages.Should().Contain(msg => msg.Contains("Downloading Synentra Intent Model Community Edition"));
        logger.LogMessages.Should().Contain(msg => msg.Contains("Model successfully downloaded"));
    }

    [Fact]
    public async Task ReleaseNotFound_ThrowsInvalidOperationException()
    {
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var httpFactory = new FakeHttpClientFactory();
        gitHub.OnGetLatestRelease = (o, r) => throw new NotFoundException("not found", System.Net.HttpStatusCode.NotFound);

        var downloader = new ModelDownloader(logger, gitHub, httpFactory);
        var config = new InternalOnnxConfiguration { PackagePath = Path.Combine(_tempDir, "model.zip"), ModelType = "Community" };

        Func<Task> act = () => downloader.EnsureModelExistsAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No release found*");
    }

    [Fact]
    public async Task AssetNotFoundInRelease_ThrowsInvalidOperationException()
    {
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var release = CreateFakeRelease(assetName: "other-file.zip", assetUrl: "http://example.com/other.zip");
        gitHub.OnGetLatestRelease = (o, r) => Task.FromResult(release);

        var downloader = new ModelDownloader(logger, gitHub, new FakeHttpClientFactory());
        var config = new InternalOnnxConfiguration { PackagePath = Path.Combine(_tempDir, "model.zip"), ModelType = "Community" };

        Func<Task> act = () => downloader.EnsureModelExistsAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Asset 'intent-model-community.zip'*not found*");
    }

    [Fact]
    public async Task WithChecksumAsset_VerifiesAndPasses()
    {
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var httpFactory = new FakeHttpClientFactory();

        // Create a release with model asset and a checksum asset
        var release = CreateFakeRelease(
            assetName: "intent-model-community.zip",
            assetUrl: "http://example.com/model.zip",
            checksumAssetName: "intent-model-community.zip.sha256",
            checksumUrl: "http://example.com/model.zip.sha256"
        );
        gitHub.OnGetLatestRelease = (o, r) => Task.FromResult(release);

        // Prepare the model content (known bytes for checksum)
        byte[] modelContent = new byte[] { 1, 2, 3, 4, 5 };
        string hash = Convert.ToHexString(SHA256.HashData(modelContent));

        // Checksum handler: returns the expected hash
        httpFactory.AddHandler("ChecksumDownloader", () => new FakeHttpMessageHandler(async (request, ct) =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent($"{hash}  intent-model-community.zip")
            };
        }));

        // Model download handler
        httpFactory.AddHandler("ModelDownloader", () => new FakeHttpMessageHandler(async (request, ct) =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(modelContent)
            };
        }));

        var downloader = new ModelDownloader(logger, gitHub, httpFactory);
        string targetPath = Path.Combine(_tempDir, "model-verified.zip");
        var config = new InternalOnnxConfiguration { PackagePath = targetPath, ModelType = "Community" };

        // Act
        await downloader.EnsureModelExistsAsync(config, CancellationToken.None);

        // Assert
        File.Exists(targetPath).Should().BeTrue();
        logger.LogMessages.Should().Contain(msg => msg.Contains("Checksum verification passed"));
    }

    [Fact]
    public async Task ChecksumMismatch_DeletesFileAndThrows()
    {
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var httpFactory = new FakeHttpClientFactory();

        var release = CreateFakeRelease(
            assetName: "intent-model-community.zip",
            assetUrl: "http://example.com/model.zip",
            checksumAssetName: "intent-model-community.zip.sha256",
            checksumUrl: "http://example.com/model.zip.sha256"
        );
        gitHub.OnGetLatestRelease = (o, r) => Task.FromResult(release);

        byte[] modelContent = new byte[] { 1, 2, 3 };
        string wrongHash = "ABCDEF1234567890ABCDEF1234567890"; // incorrect

        httpFactory.AddHandler("ChecksumDownloader", () => new FakeHttpMessageHandler(async (request, ct) =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(wrongHash)
            };
        }));
        httpFactory.AddHandler("ModelDownloader", () => new FakeHttpMessageHandler(async (request, ct) =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(modelContent)
            };
        }));

        var downloader = new ModelDownloader(logger, gitHub, httpFactory);
        string targetPath = Path.Combine(_tempDir, "model-fail.zip");
        var config = new InternalOnnxConfiguration { PackagePath = targetPath, ModelType = "Community" };

        Func<Task> act = () => downloader.EnsureModelExistsAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*Checksum verification failed*");

        // Temp file should be cleaned up
        File.Exists(targetPath).Should().BeFalse("because the corrupted file was deleted");
    }

    [Fact]
    public async Task ChecksumFileIsEmpty_ThrowsInvalidDataException()
    {
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var httpFactory = new FakeHttpClientFactory();

        var release = CreateFakeRelease(
            assetName: "intent-model-community.zip",
            assetUrl: "http://example.com/model.zip",
            checksumAssetName: "intent-model-community.zip.sha256",
            checksumUrl: "http://example.com/model.zip.sha256"
        );
        gitHub.OnGetLatestRelease = (o, r) => Task.FromResult(release);

        httpFactory.AddHandler("ChecksumDownloader", () => new FakeHttpMessageHandler(async (request, ct) =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("   ")  // empty after trim
            };
        }));

        var downloader = new ModelDownloader(logger, gitHub, httpFactory);
        var config = new InternalOnnxConfiguration { PackagePath = Path.Combine(_tempDir, "model.zip"), ModelType = "Community" };

        Func<Task> act = () => downloader.EnsureModelExistsAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*checksum file is empty*");
    }

    [Fact]
    public async Task DownloadFails_CleansUpTempFile()
    {
        var logger = new FakeLogger<ModelDownloader>();
        var gitHub = new FakeGitHubReleaseClient();
        var httpFactory = new FakeHttpClientFactory();

        var release = CreateFakeRelease(
            assetName: "intent-model-community.zip",
            assetUrl: "http://example.com/model.zip"
        );
        gitHub.OnGetLatestRelease = (o, r) => Task.FromResult(release);

        httpFactory.AddHandler("ModelDownloader", () => new FakeHttpMessageHandler(async (request, ct) =>
        {
            // Simulate network failure
            throw new HttpRequestException("Network error");
        }));

        var downloader = new ModelDownloader(logger, gitHub, httpFactory);
        string targetPath = Path.Combine(_tempDir, "model-netfail.zip");
        var config = new InternalOnnxConfiguration { PackagePath = targetPath, ModelType = "Community" };

        Func<Task> act = () => downloader.EnsureModelExistsAsync(config, CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();

        // Temp file should not exist
        File.Exists(targetPath + ".tmp").Should().BeFalse();
    }

    // Helper: construct a fake Release object with minimal properties needed
    private static Release CreateFakeRelease(
        string assetName,
        string assetUrl,
        string? checksumAssetName = null,
        string? checksumUrl = null)
    {
        var assets = new List<ReleaseAsset>
        {
            new ReleaseAsset(
                url: assetUrl,
                id: 1,
                nodeId: "abc",
                name: assetName,
                label: null,
                state: "uploaded",
                uploader: null!,
                contentType: "application/zip",
                size: 1024,
                downloadCount: 0,
                createdAt: DateTimeOffset.UtcNow,
                updatedAt: DateTimeOffset.UtcNow,
                browserDownloadUrl: assetUrl)
        };

        if (checksumAssetName != null)
        {
            assets.Add(new ReleaseAsset(
                url: checksumUrl!,
                id: 2,
                nodeId: "def",
                name: checksumAssetName,
                label: null,
                state: "uploaded",
                uploader: null!,
                contentType: "text/plain",
                size: 64,
                downloadCount: 0,
                createdAt: DateTimeOffset.UtcNow,
                updatedAt: DateTimeOffset.UtcNow,
                browserDownloadUrl: checksumUrl!));
        }

        // Using constructor that accepts assets list (available in Octokit)
        return new Release(
            url: "https://api.github.com/repos/owner/repo/releases/1",
            htmlUrl: "https://github.com/owner/repo/releases/1",
            assetsUrl: "https://api.github.com/repos/owner/repo/releases/1/assets",
            id: 1, 
            nodeId: "abc",
            uploadUrl: "",
            tagName: "v1.0",
            name: "Release 1",
            body: "",
            targetCommitish: "main",
            draft: false,
            prerelease: false,
            createdAt: DateTimeOffset.UtcNow,
            publishedAt: DateTimeOffset.UtcNow,
            author: null!,
            tarballUrl: "",
            zipballUrl: "",
            assets: assets);
    }

    // A simple HttpMessageHandler that invokes a delegate
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await _handler(request, cancellationToken);
        }
    }

    private class FakeLogger<T> : ILogger<T>
    {
        public List<string> LogMessages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            LogMessages.Add($"[{logLevel}] {msg}");
        }
    }

    private class FakeGitHubReleaseClient : IGitHubReleaseClient
    {
        public Func<string, string, Task<Release>>? OnGetLatestRelease { get; set; }

        public Task<Release> GetLatestReleaseAsync(string owner, string repo)
        {
            if (OnGetLatestRelease == null)
                throw new InvalidOperationException("No handler set.");
            return OnGetLatestRelease(owner, repo);
        }
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly Dictionary<string, Func<HttpMessageHandler>> _handlers = new();

        public void AddHandler(string name, Func<HttpMessageHandler> handlerFactory)
            => _handlers[name] = handlerFactory;

        public HttpClient CreateClient(string name)
        {
            if (_handlers.TryGetValue(name, out var handlerFactory))
                return new HttpClient(handlerFactory())
                {
                    Timeout = TimeSpan.FromMinutes(10) // match real timeout
                };
            return new HttpClient(); // fallback for unexpected requests
        }
    }
}