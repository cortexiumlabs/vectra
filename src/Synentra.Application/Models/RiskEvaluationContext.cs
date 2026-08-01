namespace Synentra.Application.Models;

public sealed record RiskEvaluationContext
{
    public required RequestContext RequestContext { get; init; }

    public required IntentClassificationResult Intent { get; init; }
}
