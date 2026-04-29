using System.Security.Cryptography;
using System.Text.Json;
using Vectra.BuildingBlocks.Configuration.Semantic;

namespace Vectra.Infrastructure.Semantic.Providers.InternalBert;

/// <summary>
/// Reads a model package from the local file system and returns all assets in memory.
/// For Community models the ONNX is used as-is; for Pro models it is decrypted in memory
/// using the AES-256-GCM key from the license file. The key is zeroed immediately after use.
/// </summary>
internal static class ModelPackageLoader
{
    internal static ModelAssets Load(InternalOnnxConfiguration config)
    {
        var packagePath = config.PackagePath
            ?? throw new InvalidOperationException("Internal ONNX model PackagePath is not configured.");

        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Model package not found.", packagePath);

        var packageBytes = File.ReadAllBytes(packagePath);
        var isPro = string.Equals(
            (config.ModelType ?? string.Empty).Trim(),
            "Pro",
            StringComparison.OrdinalIgnoreCase);

        var onnxEntryName = isPro
            ? ModelPackageExtractor.OnnxEncEntryName
            : ModelPackageExtractor.OnnxEntryName;

        var (rawOnnx, vocab, labels) = ModelPackageExtractor.Extract(packageBytes, onnxEntryName);

        if (!isPro)
            return new ModelAssets(rawOnnx, vocab, labels);

        // Pro: decrypt the ONNX bytes in memory, then zero the key.
        var key = LoadDecryptionKey(config);
        byte[] onnxBytes;
        try
        {
            onnxBytes = DecryptAes256Gcm(rawOnnx, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return new ModelAssets(onnxBytes, vocab, labels);
    }

    private static byte[] LoadDecryptionKey(InternalOnnxConfiguration config)
    {
        var licensePath = config.LicensePath
            ?? throw new InvalidOperationException("Pro model LicensePath is not configured.");

        if (!File.Exists(licensePath))
            throw new FileNotFoundException("License file not found.", licensePath);

        using var stream = new FileStream(licensePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var license = JsonSerializer.Deserialize<LicenseDocument>(stream)
            ?? throw new InvalidOperationException("License file is empty or malformed.");

        if (string.IsNullOrWhiteSpace(license.Key))
            throw new InvalidOperationException("License file does not contain a decryption key.");

        if (license.ExpiresUtc.HasValue && license.ExpiresUtc.Value < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("The Pro model license has expired.");

        return Convert.FromBase64String(license.Key);
    }

    private static byte[] DecryptAes256Gcm(byte[] cipherBlob, byte[] key)
    {
        const int NonceSize = 12;
        const int TagSize   = 16;

        if (cipherBlob.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted ONNX blob is too short to be valid.");

        var nonce      = cipherBlob.AsSpan(0, NonceSize);
        var tag        = cipherBlob.AsSpan(NonceSize, TagSize);
        var cipherText = cipherBlob.AsSpan(NonceSize + TagSize);
        var plainText  = new byte[cipherText.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipherText, tag, plainText);

        return plainText;
    }

    private sealed class LicenseDocument
    {
        public string? Key { get; init; }
        public DateTimeOffset? ExpiresUtc { get; init; }
    }
}
