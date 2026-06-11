using Vectra.Domain.Policies;

namespace Vectra.Application.Features.Simulations.SimulateDecision;

public sealed record SimulateDecisionResult(
    DecisionType Type,
    string? Reason,
    double TrustScore,
    string? PolicyName);
