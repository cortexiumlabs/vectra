using System.Text.RegularExpressions;
using Synentra.Application.Models;

namespace Synentra.Infrastructure.Risk.Calculators;

public class PathRiskCalculator : IRiskCalculator
{
    public string Name => "PathRisk";
    public double Weight { get; set; } = 0.20;

    private static readonly List<(Regex Pattern, double Risk)> PathPatterns = new()
    {
        (new Regex(@"/admin/", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3)), 0.8),
        (new Regex(@"/export|/dump|/bulk", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3)), 0.9),
        (new Regex(@"/delete|/remove|/drop", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3)), 0.85),
        (new Regex(@"/users/all|/users/export", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3)), 0.95),
        (new Regex(@"/config|/settings|/env", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3)), 0.7),
        (new Regex(@"/internal/", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3)), 0.6),
        (new Regex(@"/v[0-9]+/", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(3)), 0.2)  // versioned API slightly higher
    };

    public Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken)
    {
        var path = context.RequestContext.Path;
        double maxRisk = 0.1; // default low risk
        foreach (var (pattern, risk) in PathPatterns)
        {
            if (pattern.IsMatch(path))
            {
                maxRisk = Math.Max(maxRisk, risk);
            }
        }
        return Task.FromResult(RiskCalculatorResult.Create(Name, maxRisk, Weight));
    }
}