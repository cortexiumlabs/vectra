using Vectra.Application.Models;
using Vectra.Domain.Agents;
using Vectra.Infrastructure.Semantic;

namespace Vectra.Infrastructure.Risk.Calculators;

public class AnomalyDetectionCalculator : IRiskCalculator
{
    public string Name => "anomaly";
    public double Weight { get; set; } = 0.2;

    private readonly IAnomalyDetector _anomalyDetector; // ML service

    public AnomalyDetectionCalculator(IAnomalyDetector anomalyDetector)
    {
        _anomalyDetector = anomalyDetector;
    }

    public async Task<double> CalculateAsync(RequestContext context, AgentHistory? history, CancellationToken cancellationToken)
    {
        var anomalyScore = await _anomalyDetector.DetectAsync(context, cancellationToken);
        return anomalyScore;
    }
}