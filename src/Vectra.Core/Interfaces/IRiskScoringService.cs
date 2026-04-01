using Vectra.Core.DTOs;

namespace Vectra.Core.Interfaces;

public interface IRiskScoringService
{
    double ComputeRiskScore(RequestContext context);
}