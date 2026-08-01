using Synentra.Application.Models;

namespace Synentra.Infrastructure.Policy.Opa;

public interface IOpaInputMapper
{
    OpaDecisionInput Map(PolicyEvaluationContext context);
}
