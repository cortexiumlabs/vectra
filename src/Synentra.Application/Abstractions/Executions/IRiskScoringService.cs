using Synentra.Application.Models;

namespace Synentra.Application.Abstractions.Executions;

public interface IRiskScoringService
{
    Task<double> ComputeRiskScoreAsync(RequestContext context, CancellationToken cancellationToken = default);
}