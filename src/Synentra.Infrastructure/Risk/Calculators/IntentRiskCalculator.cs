using Synentra.Application.Models;

namespace Synentra.Infrastructure.Risk.Calculators;

public sealed class IntentRiskCalculator : IRiskCalculator
{
    private static readonly IReadOnlyDictionary<string, double> Scores =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["health_check"] = 0.05,
            ["safe_read"] = 0.10,
            ["list"] = 0.10,
            ["audit"] = 0.20,
            ["create"] = 0.35,
            ["safe_write"] = 0.40,
            ["update"] = 0.40,
            ["export"] = 0.60,
            ["configure"] = 0.60,
            ["bulk_export"] = 0.75,
            ["bulk_import"] = 0.75,
            ["admin_action"] = 0.80,
            ["suspicious"] = 0.85,
            ["destructive_delete"] = 0.90,
            ["escalate_privileges"] = 0.95,
            ["harmful"] = 1.00
        };

    public string Name => "IntentRisk";
    public double Weight { get; } = 0.25;

    public Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var intent = context.Intent;

        var baseScore = Scores.TryGetValue(intent.Label, out var configuredScore)
            ? configuredScore
            : 0.85;

        var confidenceAdjustment = intent.Status switch
        {
            IntentClassificationStatus.Classified => 0.0,
            IntentClassificationStatus.LowConfidence => 0.10,
            IntentClassificationStatus.Failed => 0.15,
            IntentClassificationStatus.Unavailable => 0.15,
            _ => 0.10
        };

        var score = Math.Clamp(baseScore + confidenceAdjustment, 0.0, 1.0);

        var signal = new RiskSignal
        {
            Code = "intent_risk",
            Description = $"Intent '{intent.Label}' with confidence {intent.Confidence:F2}."
        };

        return Task.FromResult(RiskCalculatorResult.Create(Name, score, Weight, new[] { signal }));
    }
}
