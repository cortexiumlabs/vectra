namespace Synentra.Application.Models;

public sealed record RiskSignal
{
    public required string Code { get; init; }

    public string? Description { get; init; }
}
