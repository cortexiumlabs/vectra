using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Models;
using Synentra.BuildingBlocks.Clock;
using Synentra.BuildingBlocks.Configuration.HumanInTheLoop;
using Synentra.BuildingBlocks.Configuration.Policy;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Domain.AuditTrails;
using Synentra.Domain.Policies;

namespace Synentra.Infrastructure.Decision;

public class DecisionEngine : IDecisionEngine
{
    private readonly SemanticConfiguration _semantic;
    private readonly HumanInTheLoopConfiguration _hitl;
    private readonly PolicyConfiguration _policy;

    private readonly IPolicyProvider _policyProvider;
    private readonly IRiskScoringService _riskScoring;
    private readonly ISemanticProvider _semanticProvider;
    private readonly IAgentHistoryRepository _history;
    private readonly IAuditRepository _audit;
    private readonly IClock _clock;
    private readonly ILogger<DecisionEngine> _logger;

    public DecisionEngine(
        IOptions<SemanticConfiguration> semantic,
        IOptions<HumanInTheLoopConfiguration> hitl,
        IOptions<PolicyConfiguration> policy,
        IPolicyProvider policyProvider,
        IRiskScoringService riskScoring,
        ISemanticProvider semanticProvider,
        IAgentHistoryRepository history,
        IAuditRepository audit,
        IClock clock,
        ILogger<DecisionEngine> logger)
    {
        _semantic = semantic?.Value ?? throw new ArgumentNullException(nameof(semantic));
        _hitl = hitl?.Value ?? throw new ArgumentNullException(nameof(hitl));
        _policy = policy?.Value ?? throw new ArgumentNullException(nameof(policy));

        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _riskScoring = riskScoring ?? throw new ArgumentNullException(nameof(riskScoring));
        _semanticProvider = semanticProvider ?? throw new ArgumentNullException(nameof(semanticProvider));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DecisionResult> EvaluateAsync(RequestContext context, CancellationToken cancellationToken = default)
    {
        var intentResult = await EvaluateSemanticAsync(context, cancellationToken);

        var riskResult = await _riskScoring.ComputeRiskScoreAsync(
            new RiskEvaluationContext
            {
                RequestContext = context,
                Intent = intentResult
            },
            cancellationToken);

        var policyResult = await EvaluatePolicyAsync(
            new PolicyEvaluationContext
            {
                RequestContext = context,
                Intent = intentResult,
                Risk = riskResult
            },
            cancellationToken);

        var decision = HandlePolicyDecision(intentResult, riskResult, policyResult);
        return await FinalizeAsync(context, decision, cancellationToken);
    }

    public async Task<DecisionResult> SimulateAsync(RequestContext context, CancellationToken cancellationToken = default)
    {
        var intentResult = await EvaluateSemanticAsync(context, cancellationToken);

        var riskResult = await _riskScoring.ComputeRiskScoreAsync(
            new RiskEvaluationContext
            {
                RequestContext = context,
                Intent = intentResult
            },
            cancellationToken);

        var policyResult = await EvaluatePolicyAsync(
            new PolicyEvaluationContext
            {
                RequestContext = context,
                Intent = intentResult,
                Risk = riskResult
            },
            cancellationToken);

        return HandlePolicyDecision(intentResult, riskResult, policyResult);
    }

    private async Task<PolicyDecision> EvaluatePolicyAsync(
        PolicyEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_policy.Enabled == false)
            return PolicyDecision.Allow("Policy is disabled");

        return await _policyProvider.EvaluateAsync(context, cancellationToken);
    }

    private DecisionResult HandlePolicyDecision(
        IntentClassificationResult intentResult,
        RiskEvaluationResult riskResult,
        PolicyDecision policyDecision)
    {
        policyDecision ??= PolicyDecision.Allow();

        var threshold = _hitl.Threshold ?? 0.8;

        if (policyDecision.IsDenied)
            return DecisionResult.Deny(policyDecision.Reason ?? "Policy denied", riskResult.RiskScore);

        if (policyDecision.IsHitl)
            return DecisionResult.Hitl(policyDecision.Reason ?? "Policy requires HITL", riskResult.RiskScore);

        if (intentResult.Status == IntentClassificationStatus.LowConfidence && _semantic.AllowLowConfidence != true)
            return DecisionResult.Hitl($"Low semantic confidence: {intentResult.Confidence:F2}", riskResult.RiskScore);

        if (riskResult.RiskScore > threshold)
            return DecisionResult.Hitl($"High risk score: {riskResult.RiskScore:F2}", riskResult.RiskScore);

        return DecisionResult.Allow(riskResult.RiskScore);
    }

    private async Task<IntentClassificationResult> EvaluateSemanticAsync(RequestContext context, CancellationToken ct)
    {
        if (_semantic.Enabled == false)
        {
            return new IntentClassificationResult
            {
                Label = "suspicious",
                Confidence = 0,
                Status = IntentClassificationStatus.Unavailable,
                FailureReason = "Semantic classifier is disabled"
            };
        }

        try
        {
            var result = await _semanticProvider.AnalyzeAsync(context.Body, context.Path, ct);
            var threshold = _semantic.ConfidenceThreshold ?? 0.7;

            if (result.Confidence >= threshold)
            {
                return new IntentClassificationResult
                {
                    Label = result.Intent,
                    Confidence = result.Confidence,
                    Status = IntentClassificationStatus.Classified
                };
            }

            _logger.LogWarning(
                "Low semantic confidence ({Confidence}) for intent {Intent}",
                result.Confidence,
                result.Intent);

            return new IntentClassificationResult
            {
                OriginalLabel = result.Intent,
                Label = "suspicious",
                Confidence = result.Confidence,
                Status = IntentClassificationStatus.LowConfidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic classification failed for agent {AgentId}", context.AgentId);

            return new IntentClassificationResult
            {
                Label = "suspicious",
                Confidence = 0,
                Status = IntentClassificationStatus.Failed,
                FailureReason = ex.Message
            };
        }
    }

    private async Task<DecisionResult> FinalizeAsync(
        RequestContext context,
        DecisionResult decision,
        CancellationToken ct)
    {
        await RecordHistoryAsync(context, decision, ct);
        await RecordAuditAsync(context, decision, ct);
        return decision;
    }

    private async Task RecordHistoryAsync(
        RequestContext context,
        DecisionResult decision,
        CancellationToken cancellationToken = default)
    {
        var violation = decision.IsDenied || decision.IsHitl;

        try
        {
            await _history.RecordRequestAsync(
                context.AgentId,
                violation,
                decision.TrustScore,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to record agent history for {AgentId}",
                context.AgentId);
        }
    }

    private async Task RecordAuditAsync(
        RequestContext context,
        DecisionResult decision,
        CancellationToken cancellationToken = default)
    {
        var audit = new AuditTrail
        {
            AgentId = context.AgentId,
            Action = $"{context.Method} {context.Path}",
            TargetUrl = context.Path,
            Status = decision.Type.ToString(),
            RiskScore = decision.TrustScore, // FIXED
            Intent = context.Body,
            Reason = decision.Reason,
            Timestamp = _clock.UtcNow
        };

        await _audit.AddAsync(audit, cancellationToken);
    }
}