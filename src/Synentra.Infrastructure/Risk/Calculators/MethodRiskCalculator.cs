using Synentra.Application.Models;
namespace Synentra.Infrastructure.Risk.Calculators;

public class MethodRiskCalculator : IRiskCalculator
{
    public string Name => "MethodRisk";
    public double Weight { get; set; } = 0.15;

    private static readonly Dictionary<string, double> MethodRisk = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"] = 0.1,
        ["HEAD"] = 0.05,
        ["OPTIONS"] = 0.05,
        ["POST"] = 0.4,
        ["PUT"] = 0.6,
        ["PATCH"] = 0.5,
        ["DELETE"] = 0.9,
        ["TRACE"] = 0.7,
        ["CONNECT"] = 0.8
    };

    public Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken)
    {
        var method = context.RequestContext.Method;
        var risk = MethodRisk.TryGetValue(method, out var value) ? value : 0.5;
        return Task.FromResult(RiskCalculatorResult.Create(Name, risk, Weight));
    }
}