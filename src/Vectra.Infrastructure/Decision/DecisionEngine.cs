using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vectra.Application.Abstractions.Executions;
using Vectra.Application.Abstractions.Persistence;
using Vectra.Application.Models;
using Vectra.BuildingBlocks.Clock;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;
using Vectra.BuildingBlocks.Configuration.Policy;
using Vectra.BuildingBlocks.Configuration.Semantic;
using Vectra.Domain.AuditTrails;
using Vectra.Domain.Policies;

namespace Vectra.Infrastructure.Decision;

public class DecisionEngine : IDecisionEngine
{
    private readonly IOptions<SemanticConfiguration> _semanticOptions;
    private readonly IOptions<HumanInTheLoopConfiguration> _hitlOptions;
    private readonly IOptions<PolicyConfiguration> _policyOptions;
    private readonly IPolicyProvider _policyProvider;
    private readonly IRiskScoringService _riskScoring;
    private readonly ISemanticProvider _semanticProvider;
    private readonly IAgentHistoryRepository _historyRecorder;
    private readonly IAuditRepository _auditRepository;
    private readonly IClock _clock;
    private readonly ILogger<DecisionEngine> _logger;

    public DecisionEngine(
        IOptions<SemanticConfiguration> options,
        IOptions<HumanInTheLoopConfiguration> hitlOptions,
        IOptions<PolicyConfiguration> policyOptions,
        IPolicyProvider policyEngine, 
        IRiskScoringService riskScoring, 
        ISemanticProvider semanticProvider,
        IAgentHistoryRepository historyRecorder,
        IAuditRepository auditRepository,
        IClock clock,
        ILogger<DecisionEngine> logger)
    {
        _semanticOptions = options ?? throw new ArgumentNullException(nameof(options));
        _hitlOptions = hitlOptions ?? throw new ArgumentNullException(nameof(hitlOptions));
        _policyOptions = policyOptions ?? throw new ArgumentNullException(nameof(policyOptions));
        _policyProvider = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
        _riskScoring = riskScoring ?? throw new ArgumentNullException(nameof(riskScoring));
        _semanticProvider = semanticProvider ?? throw new ArgumentNullException(nameof(semanticProvider));
        _historyRecorder = historyRecorder ?? throw new ArgumentNullException(nameof(historyRecorder));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DecisionResult> EvaluateAsync(RequestContext context, CancellationToken cancellationToken)
    {
        var policyInput = new Dictionary<string, object>
        {
            ["method"] = context.Method,
            ["path"] = context.Path,
            ["headers"] = context.Headers,
            ["agent"] = new Dictionary<string, object>
            {
                ["id"] = context.AgentId,
                ["trust_score"] = context.TrustScore
            }
        };

        var policyEnabled = _policyOptions.Value.Enabled ?? true;
        if (policyEnabled)
        {
            var policyDecision = await _policyProvider.EvaluateAsync(context.PolicyName, policyInput, cancellationToken);
            if (policyDecision.IsDenied || policyDecision.IsHitl)
            {
                var decision = policyDecision.IsDenied 
                    ? DecisionResult.Deny(policyDecision.Reason ?? "Policy denied", 0.0)
                    : DecisionResult.Hitl(policyDecision.Reason ?? "Policy requires HITL", 0.0);
                await RecordHistoryAsync(context, decision, cancellationToken);
                await RecordAuditAsync(context, decision, cancellationToken);
                return decision;
            }
        }

        var riskScore = await _riskScoring.ComputeRiskScoreAsync(context, cancellationToken);
        var hitlThreshold = _hitlOptions.Value.Threshold ?? 0.8;
        if (riskScore > hitlThreshold)
        {
            var riskDecision = DecisionResult.Hitl($"High risk score: {riskScore:F2}", riskScore);
            await RecordHistoryAsync(context, riskDecision, cancellationToken);
            await RecordAuditAsync(context, riskDecision, cancellationToken);
            return riskDecision;
        }

        var semanticEnabled = _semanticOptions.Value.Enabled ?? true;
        SemanticAnalysisResult? semantic = null;
        if (semanticEnabled)
        {
            semantic = await _semanticProvider.AnalyzeAsync(context.Body, context.Path, cancellationToken);
            var confidenceThreshold = _semanticOptions.Value.ConfidenceThreshold ?? 0.7;

            if (semantic.Confidence < confidenceThreshold)
            {
                if (_semanticOptions.Value.AllowLowConfidence == true)
                {
                    _logger.LogWarning("Low confidence semantic ({Confidence}), but allowing due to configuration", semantic.Confidence);
                }
                else
                {
                    var semanticDecision = DecisionResult.Hitl($"Low semantic confidence: {semantic.Confidence:F2}", semantic.Confidence);
                    await RecordHistoryAsync(context, semanticDecision, cancellationToken);
                    await RecordAuditAsync(context, semanticDecision, cancellationToken);
                    return semanticDecision;
                }
            }
        }

        var allowDecision = DecisionResult.Allow(riskScore);
        await RecordHistoryAsync(context, allowDecision, cancellationToken);
        await RecordAuditAsync(context, allowDecision, cancellationToken);
        return allowDecision;
    }

    private async Task RecordHistoryAsync(
        RequestContext context, 
        DecisionResult decision,
        CancellationToken ct)
    {
        var wasViolation = decision.IsDenied || decision.IsHitl;
        try
        {
            await _historyRecorder.RecordRequestAsync(context.AgentId, wasViolation, decision.TrustScore, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record agent history for {AgentId}", context.AgentId);
        }
    }

    private async Task RecordAuditAsync(
        RequestContext context, 
        DecisionResult decision, 
        CancellationToken cancellationToken)
    {
        var auditLog = new AuditTrail
        {
            AgentId = context.AgentId,
            Action = $"{context.Method} {context.Path}",
            TargetUrl = context.Path,
            Status = decision.Type.ToString(),
            RiskScore = context.TrustScore,
            Intent = context.Body,
            Reason = decision.Reason,
            Timestamp = null
        };
        await _auditRepository.AddAsync(auditLog, cancellationToken);
    }
}