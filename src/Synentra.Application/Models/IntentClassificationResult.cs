namespace Synentra.Application.Models;

public sealed record IntentClassificationResult
{
    public required string Label { get; init; }

    public required double Confidence { get; init; }

    public required IntentClassificationStatus Status { get; init; }

    public string? ModelVersion { get; init; }

    public string? OriginalLabel { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyCollection<IntentPrediction> Alternatives { get; init; } = Array.Empty<IntentPrediction>();
}
