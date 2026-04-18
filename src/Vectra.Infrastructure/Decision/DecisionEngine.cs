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
        // 1. Policy
        var policyDecision = await EvaluatePolicyAsync(context, cancellationToken);
        if (policyDecision != null)
            return await FinalizeAsync(context, policyDecision, cancellationToken);

        // 2. Risk
        var riskScore = await _riskScoring.ComputeRiskScoreAsync(context, cancellationToken);
        var riskDecision = EvaluateRisk(riskScore);
        if (riskDecision != null)
            return await FinalizeAsync(context, riskDecision, cancellationToken);

        // 3. Semantic
        var semanticDecision = await EvaluateSemanticAsync(context, cancellationToken);
        if (semanticDecision != null)
            return await FinalizeAsync(context, semanticDecision, cancellationToken);

        // 4. Allow
        return await FinalizeAsync(context, DecisionResult.Allow(riskScore), cancellationToken);
    }

    private async Task<DecisionResult?> EvaluatePolicyAsync(RequestContext context, CancellationToken cancellationToken = default)
    {
        if (_policy.Enabled == false)
            return null;

        var input = BuildPolicyInput(context);
        var result = await _policyProvider.EvaluateAsync(context.PolicyName, input, cancellationToken);

        if (result.IsDenied)
            return DecisionResult.Deny(result.Reason ?? "Policy denied", 0);

        if (result.IsHitl)
            return DecisionResult.Hitl(result.Reason ?? "Policy requires HITL", 0);

        return null;
    }

    private DecisionResult? EvaluateRisk(double riskScore)
    {
        var threshold = _hitl.Threshold ?? 0.8;

        if (riskScore > threshold)
            return DecisionResult.Hitl($"High risk score: {riskScore:F2}", riskScore);

        return null;
    }

    private async Task<DecisionResult?> EvaluateSemanticAsync(RequestContext context, CancellationToken ct)
    {
        if (_semantic.Enabled == false)
            return null;

        var result = await _semanticProvider.AnalyzeAsync(context.Body, context.Path, ct);
        var threshold = _semantic.ConfidenceThreshold ?? 0.7;

        if (result.Confidence >= threshold)
            return null;

        if (_semantic.AllowLowConfidence == true)
        {
            _logger.LogWarning(
                "Low semantic confidence ({Confidence}) allowed by configuration",
                result.Confidence);
            return null;
        }

        return DecisionResult.Hitl(
            $"Low semantic confidence: {result.Confidence:F2}",
            result.Confidence);
    }

    private static Dictionary<string, object> BuildPolicyInput(RequestContext context)
        => new()
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