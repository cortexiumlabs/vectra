using Synentra.Application.Models;

namespace Synentra.Application.Abstractions.Executions;

public interface IRiskScoringService
{
    Task<RiskEvaluationResult> ComputeRiskScoreAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default);
}