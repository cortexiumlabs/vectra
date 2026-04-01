using Vectra.Core.DTOs;
using Vectra.Core.Entities;
using Vectra.Core.Interfaces;

namespace Vectra.Core.UseCases;

public class EvaluateRequestUseCase
{
    private readonly IDecisionEngine _decisionEngine;
    private readonly IAuditRepository _auditRepository;

    public EvaluateRequestUseCase(IDecisionEngine decisionEngine, IAuditRepository auditRepository)
    {
        _decisionEngine = decisionEngine;
        _auditRepository = auditRepository;
    }

    public async Task<DecisionResult> ExecuteAsync(RequestContext context, CancellationToken cancellationToken = default)
    {
        var decision = await _decisionEngine.EvaluateAsync(context, cancellationToken);

        // Log the decision
        var auditLog = new AuditLog(
            context.AgentId,
            $"{context.Method} {context.Path}",
            context.Path,
            decision.Type.ToString(),
            context.TrustScore, // or a computed risk score
            "", // intent would come from semantic engine
            null);
        await _auditRepository.AddAsync(auditLog, cancellationToken);

        return decision;
    }
}