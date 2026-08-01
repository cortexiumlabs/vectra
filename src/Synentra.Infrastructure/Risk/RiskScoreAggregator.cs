using Microsoft.Extensions.Options;
using Synentra.Application.Models;
using Synentra.BuildingBlocks.Configuration.Risk;

namespace Synentra.Infrastructure.Risk;

public class RiskScoreAggregator
{
    private readonly IEnumerable<IRiskCalculator> _calculators;
    private readonly RiskConfiguration _configuration;

    public RiskScoreAggregator(
        IEnumerable<IRiskCalculator> calculators,
        IOptions<RiskConfiguration> configuration)
    {
        _calculators = calculators;
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
    }

    public RiskScoreAggregator(IEnumerable<IRiskCalculator> calculators)
        : this(calculators, Options.Create(new RiskConfiguration()))
    {
    }

    public async Task<IReadOnlyCollection<RiskCalculatorResult>> AggregateAsync(
        RiskEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var calculators = _calculators.ToList();

        var tasks = calculators
            .Select(c => c.CalculateAsync(context, cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        return calculators
            .Zip(results, (calculator, result) =>
            {
                result ??= RiskCalculatorResult.Create(
                    calculator.Name,
                    score: 0,
                    weight: calculator.Weight,
                    signals: Array.Empty<RiskSignal>());

                var configuredWeight = ResolveWeight(calculator, result.Weight);
                return result with { Weight = configuredWeight };
            })
            .ToArray();
    }

    private double ResolveWeight(IRiskCalculator calculator, double fallback)
        => _configuration.Weights.GetWeight(calculator.Name) ?? fallback;
}