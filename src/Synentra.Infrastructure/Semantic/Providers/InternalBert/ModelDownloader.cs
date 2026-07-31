using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Octokit;
using Synentra.BuildingBlocks.Configuration.Semantic;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

public class ModelDownloader : IModelDownloader
{
    private readonly ILogger<ModelDownloader> _logger;

    public ModelDownloader(ILogger<ModelDownloader> logger)
    {
        _logger = logger;
    }

    public async Task EnsureModelExistsAsync(
        InternalOnnxConfiguration config,
        CancellationToken cancellationToken)
    {
        string fullPath = ModelPathResolver.GetFullPackagePath(config.PackagePath);

        if (File.Exists(fullPath))
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.Directory) == 0)
            {
                _logger.LogDebug("Model already exists at {Path}", fullPath);
                return;
            }
        }

        const string owner = "synentra";
        const string repo = "synentra-intent-community";
        const string displayModelName = "Synentra Intent Model Community Edition";

        _logger.LogInformation(
            "Downloading {ModelName} from {Owner}/{Repo} latest release...",
            displayModelName, owner, repo);

        var client = new GitHubClient(new ProductHeaderValue("Synentra"));
        Release release;
        try
        {
            release = await client.Repository.Release.GetLatest(owner, repo);
        }
        catch (NotFoundException)
        {
            throw new InvalidOperationException($"No release found for repository {owner}/{repo}.");
        }

        // Locate the model asset
        const string assetName = "intent-model-community.zip";
        var asset = release.Assets.FirstOrDefault(a =>
            a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Asset '{assetName}' not found in the latest release of {owner}/{repo}.");

        // Try to locate a checksum asset
        string checksumAssetName = assetName + ".sha256";
        var checksumAsset = release.Assets.FirstOrDefault(a =>
            a.Name.Equals(checksumAssetName, StringComparison.OrdinalIgnoreCase));

        string? expectedHash = null;
        if (checksumAsset != null)
        {
            // Download the checksum file (tiny, so no temp file needed)
            expectedHash = await DownloadChecksumAsync(checksumAsset.BrowserDownloadUrl, cancellationToken);
            _logger.LogDebug("Checksum file found. Expected hash: {Hash}", expectedHash);
        }
        else
        {
            _logger.LogWarning(
                "No checksum asset '{ChecksumAsset}' found. Skipping integrity verification.",
                checksumAssetName);
        }

        // Prepare download
        string sizeText = FormatSize(asset.Size);
        _logger.LogInformation("Downloading {ModelName} ({Size})...", displayModelName, sizeText);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Synentra");
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var tempFile = fullPath + ".tmp";
        try
        {
            using var response = await httpClient.GetAsync(
                asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(
                tempFile, System.IO.FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 81920, useAsync: true);
            await response.Content.CopyToAsync(fileStream, cancellationToken);
        }
        catch
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            throw;
        }

        // Verify checksum if we have one
        if (expectedHash != null)
        {
            bool valid = await VerifyChecksumAsync(tempFile, expectedHash, cancellationToken);
            if (!valid)
            {
                File.Delete(tempFile);
                throw new InvalidDataException(
                    $"Checksum verification failed for the downloaded model. The file '{assetName}' may be corrupted.");
            }
            _logger.LogInformation("Checksum verification passed.");
        }

        File.Move(tempFile, fullPath);
        _logger.LogInformation("Model successfully downloaded to {Path}", fullPath);
    }

    /// <summary>
    /// Downloads the content of a checksum file and returns the expected SHA‑256 hash (hex string).
    /// </summary>
    private async Task<string> DownloadChecksumAsync(string url, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Synentra");
        string content = await httpClient.GetStringAsync(url, ct);
        // Typical format: "<hash>  <filename>" or just "<hash>"
        string? hash = content.Trim().Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(hash))
            throw new InvalidDataException("The checksum file is empty or malformed.");
        return hash.Trim();
    }

    /// <summary>
    /// Computes the SHA‑256 hash of a file and compares it to the expected hex string.
    /// </summary>
    private async Task<bool> VerifyChecksumAsync(string filePath, string expectedHex, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        byte[] hashBytes = await sha256.ComputeHashAsync(stream, ct);
        string actualHex = Convert.ToHexString(hashBytes);
        return string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    // Helper: converts byte count to a readable string (e.g., "250 MB")
    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}