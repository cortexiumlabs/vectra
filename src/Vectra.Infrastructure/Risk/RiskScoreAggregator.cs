using Vectra.Application.Models;
using Vectra.Domain.Agents;

namespace Vectra.Infrastructure.Risk;

public class RiskScoreAggregator
{
    private readonly IEnumerable<IRiskCalculator> _calculators;

    public RiskScoreAggregator(IEnumerable<IRiskCalculator> calculators)
    {
        _calculators = calculators;
    }

    public async Task<double> AggregateAsync(RequestContext context, AgentHistory? history, CancellationToken ct)
    {
        var tasks = _calculators.Select(c => c.CalculateAsync(context, history, ct));
        var results = await Task.WhenAll(tasks);
        double totalWeight = 0;
        double weightedSum = 0;
        for (int i = 0; i < _calculators.Count(); i++)
        {
            var calc = _calculators.ElementAt(i);
            totalWeight += calc.Weight;
            weightedSum += results[i] * calc.Weight;
        }
        if (totalWeight == 0) return 0;
        var finalScore = weightedSum / totalWeight;
        return Math.Clamp(finalScore, 0, 1);
    }
}