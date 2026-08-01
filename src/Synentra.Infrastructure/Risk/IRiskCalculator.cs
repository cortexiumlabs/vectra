using Synentra.Application.Models;
using Synentra.Domain.Agents;
namespace Synentra.Infrastructure.Risk;

public interface IRiskCalculator
{
    string Name { get; }
    double Weight { get; }  // fallback when no configured weight exists
    Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken);
}