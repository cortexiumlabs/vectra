using Synentra.Application.Models;
using Synentra.Domain.Policies;

namespace Synentra.Application.Abstractions.Executions;

public interface IDecisionEngine
{
    Task<DecisionResult> EvaluateAsync(
        string semanticInput, 
        RequestContext context, 
        CancellationToken cancellationToken = default);

    Task<DecisionResult> SimulateAsync(
        string semanticInput, 
        RequestContext context, 
        CancellationToken cancellationToken = default);
}