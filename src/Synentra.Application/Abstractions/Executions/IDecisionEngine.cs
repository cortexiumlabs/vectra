using Synentra.Application.Models;
using Synentra.Domain.Policies;

namespace Synentra.Application.Abstractions.Executions;

public interface IDecisionEngine
{
    Task<DecisionResult> EvaluateAsync(RequestContext context, CancellationToken cancellationToken = default);

    Task<DecisionResult> SimulateAsync(RequestContext context, CancellationToken cancellationToken = default);
}