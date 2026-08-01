namespace Synentra.Application.Models;

public sealed record IntentPrediction
{
    public required string Label { get; init; }

    public required double Confidence { get; init; }
}
