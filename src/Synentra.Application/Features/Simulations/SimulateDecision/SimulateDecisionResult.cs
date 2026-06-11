using Synentra.Domain.Policies;

namespace Synentra.Application.Features.Simulations.SimulateDecision;

public sealed record SimulateDecisionResult(
    DecisionType Type,
    string? Reason,
    double TrustScore,
    string? PolicyName);
