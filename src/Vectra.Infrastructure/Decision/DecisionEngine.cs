using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vectra.Application.Abstractions.Executions;
using Vectra.Application.Abstractions.Persistence;
using Vectra.Application.Models;
using Vectra.BuildingBlocks.Configuration.Features;
using Vectra.Domain.Policies;

namespace Vectra.Infrastructure.Decision;

public class DecisionEngine : IDecisionEngine
{
    private readonly IOptions<FeaturesConfiguration> _options;
    private readonly IPolicyProvider _policyProvider;
    private readonly IRiskScoringService _riskScoring;
    private readonly ISemanticEngine _semanticEngine;
    private readonly IAgentHistoryRepository _historyRecorder;
    private readonly ILogger<DecisionEngine> _logger;

    public DecisionEngine(
        IOptions<FeaturesConfiguration> options,
        IPolicyProvider policyEngine, 
        IRiskScoringService riskScoring, 
        ISemanticEngine semanticEngine,
        IAgentHistoryRepository historyRecorder,
        ILogger<DecisionEngine> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _policyProvider = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
        _riskScoring = riskScoring ?? throw new ArgumentNullException(nameof(riskScoring));
        _semanticEngine = semanticEngine ?? throw new ArgumentNullException(nameof(semanticEngine));
        _historyRecorder = historyRecorder ?? throw new ArgumentNullException(nameof(historyRecorder));
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

        var policyEnabled = _options.Value.Policy?.Enabled ?? true;
        if (policyEnabled)
        {
            var policyDecision = await _policyProvider.EvaluateAsync(context.PolicyName, policyInput, cancellationToken);
            if (policyDecision.IsDenied || policyDecision.IsHitl)
            {
                var decision = policyDecision.IsDenied 
                    ? DecisionResult.Deny(policyDecision.Reason ?? "Policy denied")
                    : DecisionResult.Hitl(policyDecision.Reason ?? "Policy requires HITL");
                await RecordHistoryAsync(context, decision, 0, cancellationToken);
                return decision;
            }
        }

        var riskScore = await _riskScoring.ComputeRiskScoreAsync(context, cancellationToken);
        var hitlThreshold = _options.Value.Hitl?.HitlThreshold ?? 0.8;
        if (riskScore > hitlThreshold)
        {
            var riskDecision = DecisionResult.Hitl($"High risk score: {riskScore:F2}");
            await RecordHistoryAsync(context, riskDecision, riskScore, cancellationToken);
            return riskDecision;
        }

        var semanticEnabled = _options.Value.Semantic?.Enabled ?? true;
        SemanticResult? semantic = null;
        if (semanticEnabled)
        {
            semantic = await _semanticEngine.AnalyzeAsync(context.Body, context.Path, cancellationToken);
            var confidenceThreshold = _options.Value.Semantic?.ConfidenceThreshold ?? 0.7;
            if (semantic.Confidence < confidenceThreshold)
            {
                var semanticDecision = DecisionResult.Hitl($"Low semantic confidence: {semantic.Confidence:F2}");
                await RecordHistoryAsync(context, semanticDecision, riskScore, cancellationToken);
                return semanticDecision;
            }
        }

        var allowDecision = DecisionResult.Allow();
        await RecordHistoryAsync(context, allowDecision, riskScore, cancellationToken);
        return allowDecision;









        //var policyEnabled = _options.Value.Policy.Enabled ?? true;
        //if (policyEnabled)
        //{
        //    var policyDecision = await _policyProvider.EvaluateAsync(context.PolicyName, input);
        //    if (policyDecision.IsDenied)
        //        return DecisionResult.Deny(policyDecision.Reason ?? "Policy denied");
        //    if (policyDecision.IsHitl)
        //        return DecisionResult.Hitl(policyDecision.Reason ?? "Policy requires HITL");
        //}

        //var wasViolation = decision.IsDenied || decision.IsHitl;
        //var riskScore = await _riskScoring.ComputeRiskScoreAsync(context, cancellationToken);
        //await _historyRecorder.RecordRequestAsync(context.AgentId, wasViolation, riskScore, cancellationToken);

        //if (riskScore > _config.GetValue<double>("Risk:HitlThreshold", 0.8))
        //    return DecisionResult.Hitl($"High risk score: {riskScore}");

        //var semantic = await _semanticEngine.AnalyzeAsync(context.Body, context.Path, cancellationToken);
        //if (semantic.Confidence < 0.7)
        //    return DecisionResult.Hitl($"Low semantic confidence: {semantic.Confidence}");

        //return DecisionResult.Allow();
    }

    private async Task RecordHistoryAsync(RequestContext context, DecisionResult decision, double riskScore, CancellationToken ct)
    {
        var wasViolation = decision.IsDenied || decision.IsHitl;
        try
        {
            await _historyRecorder.RecordRequestAsync(context.AgentId, wasViolation, riskScore, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record agent history for {AgentId}", context.AgentId);
        }
    }
}