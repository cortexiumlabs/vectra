using Synentra.Application.Models;

namespace Synentra.Infrastructure.Policy.Opa;

public sealed class OpaInputMapper : IOpaInputMapper
{
    public OpaDecisionInput Map(PolicyEvaluationContext context)
    {
        var request = context.RequestContext;
        var intent = context.Intent;
        var risk = context.Risk;

        request.Headers.TryGetValue("Content-Type", out var contentType);

        return new OpaDecisionInput
        {
            Agent = new OpaDecisionInput.AgentInput
            {
                Id = request.AgentId,
                Roles = Array.Empty<string>(),
                Scopes = Array.Empty<string>(),
                TrustScore = risk.TrustScore
            },
            Request = new OpaDecisionInput.RequestInput
            {
                Method = request.Method,
                Path = request.Path,
                ContentType = contentType,
                ContentLength = request.Body?.Length ?? 0
            },
            Intent = new OpaDecisionInput.IntentInput
            {
                Label = intent.Label,
                OriginalLabel = intent.OriginalLabel,
                Confidence = intent.Confidence,
                Status = intent.Status.ToString(),
                ModelVersion = intent.ModelVersion
            },
            Risk = new OpaDecisionInput.RiskInput
            {
                Score = risk.RiskScore,
                Level = risk.RiskLevel,
                Signals = risk.Signals.Select(x => x.Code).ToArray()
            }
        };
    }
}
