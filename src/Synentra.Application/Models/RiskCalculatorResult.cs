namespace Synentra.Application.Models;

public sealed record RiskCalculatorResult
{
    public required string Name { get; init; }

    public required double Score { get; init; }

    public required double Weight { get; init; }

    public IReadOnlyCollection<RiskSignal> Signals { get; init; } = Array.Empty<RiskSignal>();

    public static RiskCalculatorResult Create(
        string name,
        double score,
        double weight,
        IEnumerable<RiskSignal>? signals = null)
        => new()
        {
            Name = name,
            Score = score,
            Weight = weight,
            Signals = signals?.ToArray() ?? Array.Empty<RiskSignal>()
        };
}
