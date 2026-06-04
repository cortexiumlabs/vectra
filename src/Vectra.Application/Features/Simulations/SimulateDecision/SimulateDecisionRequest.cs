using Vectra.Application.Abstractions.Dispatchers;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Simulations.SimulateDecision;

public sealed record SimulateDecisionRequest(
    string Method,
    string Path,
    string? TargetUrl,
    string? PolicyName,
    Dictionary<string, string>? Headers,
    string? ContentType,
    string? Body) : IRequest<Result<SimulateDecisionResult>>
{
    public Guid? AgentId { get; init; }
}
