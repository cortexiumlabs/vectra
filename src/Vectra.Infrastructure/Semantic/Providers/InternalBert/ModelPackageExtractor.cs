using System.IO.Compression;
using System.Text.Json;

namespace Vectra.Infrastructure.Semantic.Providers.InternalBert;

/// <summary>
/// Shared helpers for extracting a model package in memory.
/// </summary>
internal static class ModelPackageExtractor
{
    internal const string OnnxEntryName    = "model.onnx";
    internal const string OnnxEncEntryName = "model.onnx.enc";
    private  const string VocabEntry       = "vocab.txt";
    private  const string LabelsEntry      = "labels.json";

    /// <summary>
    /// Extracts raw (possibly encrypted) ONNX bytes plus vocab lines and intent labels
    /// from an in-memory package buffer.
    /// </summary>
    /// <param name="packageBuffer">Raw ZIP bytes.</param>
    /// <param name="onnxEntryName">Entry name to look for inside the ZIP (e.g. "model.onnx" or "model.onnx.enc").</param>
    internal static (byte[] RawOnnx, string[] VocabLines, string[] IntentLabels) Extract(
        ReadOnlyMemory<byte> packageBuffer,
        string onnxEntryName = OnnxEntryName)
    {
        using var ms     = new MemoryStream(packageBuffer.ToArray(), writable: false);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

        byte[]?   rawOnnx  = null;
        string[]? vocab    = null;
        string[]? labels   = null;

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.Equals(onnxEntryName, StringComparison.OrdinalIgnoreCase))
            {
                rawOnnx = ReadEntry(entry);
            }
            else if (entry.FullName.Equals(VocabEntry, StringComparison.OrdinalIgnoreCase))
            {
                vocab = ReadLines(entry);
            }
            else if (entry.FullName.Equals(LabelsEntry, StringComparison.OrdinalIgnoreCase))
            {
                labels = ParseLabels(entry);
            }
        }

        if (rawOnnx is null)  throw new InvalidOperationException($"Model package is missing '{onnxEntryName}'.");
        if (vocab is null)    throw new InvalidOperationException($"Model package is missing '{VocabEntry}'.");
        if (labels is null)   throw new InvalidOperationException($"Model package is missing '{LabelsEntry}'.");

        return (rawOnnx, vocab, labels);
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string[] ReadLines(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
            lines.Add(line);
        return lines.ToArray();
    }

    private static string[] ParseLabels(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var list = JsonSerializer.Deserialize<List<string>>(stream)
                   ?? throw new InvalidOperationException("labels.json is empty or malformed.");
        return list.ToArray();
    }
}
