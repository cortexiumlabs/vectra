using Synentra.Application.Models;

namespace Synentra.Infrastructure.Risk.Calculators;

public class BodySizeRiskCalculator : IRiskCalculator
{
    public string Name => "BodySizeRisk";
    public double Weight { get; set; } = 0.05;

    public Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken)
    {
        var body = context.RequestContext.Body;

        if (string.IsNullOrEmpty(body))
            return Task.FromResult(RiskCalculatorResult.Create(Name, 0.0, Weight));

        var size = body.Length;
        if (size > 1024 * 1024) return Task.FromResult(RiskCalculatorResult.Create(Name, 0.8, Weight));      // >1MB
        if (size > 100 * 1024) return Task.FromResult(RiskCalculatorResult.Create(Name, 0.5, Weight));       // >100KB
        if (size > 10 * 1024) return Task.FromResult(RiskCalculatorResult.Create(Name, 0.2, Weight));        // >10KB
        return Task.FromResult(RiskCalculatorResult.Create(Name, 0.0, Weight));
    }
}