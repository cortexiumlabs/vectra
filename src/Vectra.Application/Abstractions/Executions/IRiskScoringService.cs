using Vectra.Application.Models;

namespace Vectra.Application.Abstractions.Executions;

public interface IRiskScoringService
{
    double ComputeRiskScore(RequestContext context);
}