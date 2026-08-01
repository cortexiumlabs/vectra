using Synentra.Application.Models;
using Synentra.Infrastructure.Semantic;

namespace Synentra.Infrastructure.Risk.Calculators;

public class AnomalyDetectionCalculator : IRiskCalculator
{
    public string Name => "AnomalyRisk";
    public double Weight { get; set; } = 0.15;

    private readonly IAnomalyDetector _anomalyDetector; // ML service

    public AnomalyDetectionCalculator(IAnomalyDetector anomalyDetector)
    {
        _anomalyDetector = anomalyDetector;
    }

    public async Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken)
    {
        var anomalyScore = await _anomalyDetector.DetectAsync(context.RequestContext, cancellationToken);
        return RiskCalculatorResult.Create(Name, anomalyScore, Weight);
    }
}