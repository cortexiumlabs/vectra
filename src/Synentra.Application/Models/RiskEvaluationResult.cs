namespace Synentra.Application.Models;

public sealed record RiskEvaluationResult
{
    public required double RiskScore { get; init; }

    public required double TrustScore { get; init; }

    public required string RiskLevel { get; init; }

    public IReadOnlyCollection<RiskCalculatorResult> Calculators { get; init; } = Array.Empty<RiskCalculatorResult>();

    public IReadOnlyCollection<RiskSignal> Signals { get; init; } = Array.Empty<RiskSignal>();
}
