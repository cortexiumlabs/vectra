namespace Vectra.Infrastructure.Semantic.Providers.InternalBert;

internal sealed record ModelAssets(
    ReadOnlyMemory<byte> OnnxBytes,
    string[] VocabLines,
    string[] IntentLabels);
