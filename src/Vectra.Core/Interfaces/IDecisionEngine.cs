using Vectra.Core.DTOs;
using Vectra.Core.Entities;

namespace Vectra.Core.Interfaces;

public interface IDecisionEngine
{
    Task<DecisionResult> EvaluateAsync(RequestContext context, CancellationToken cancellationToken = default);
}