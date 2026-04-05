using Vectra.Application.Models;
using Vectra.Domain.Policies;

namespace Vectra.Application.Abstractions.Executions;

public interface IDecisionEngine
{
    Task<DecisionResult> EvaluateAsync(RequestContext context, CancellationToken cancellationToken = default);
}