namespace Synentra.Application.Models;

public sealed record PolicyEvaluationContext
{
    public required RequestContext RequestContext { get; init; }

    public required IntentClassificationResult Intent { get; init; }

    public required RiskEvaluationResult Risk { get; init; }
}
