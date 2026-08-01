using Synentra.Application.Models;

namespace Synentra.Infrastructure.Risk.Calculators;

public class TimeBasedCalculator : IRiskCalculator
{
    public string Name => "TimeBasedRisk";
    public double Weight { get; set; } = 0.05;

    public Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hour = now.Hour;
        var isWeekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;

        double risk = 0.0;
        if (isWeekend) risk += 0.2;
        if (hour < 6 || hour > 20) risk += 0.3; // night time
        else if (hour < 8 || hour > 18) risk += 0.1; // early morning / late evening

        return Task.FromResult(RiskCalculatorResult.Create(Name, Math.Min(0.5, risk), Weight));
    }
}
