using Microsoft.Extensions.Logging;
using Synentra.Application.Abstractions.Caches;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Models;
using Synentra.Domain.Policies;
using Synentra.Infrastructure.Caches;

namespace Synentra.Infrastructure.Policy.Providers;

public class InternalPolicyProvider : IPolicyProvider
{
    private readonly ICacheProvider _cacheProvider;
    private readonly IPolicyLoader _loader;
    private const string CacheKey = "all_policies";

    public InternalPolicyProvider(
        ICacheService cacheService,
        IPolicyLoader loader)
    {
        _cacheProvider = cacheService.Current ?? throw new ArgumentNullException(nameof(cacheService));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public async Task<PolicyDecision> EvaluateAsync(
        PolicyEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var policyName = context.RequestContext.PolicyName;

        if (string.IsNullOrEmpty(policyName))
            return PolicyDecision.Allow("No policy assigned");

        var policy = await GetPolicyAsync(policyName, cancellationToken);
        if (policy == null)
            return PolicyDecision.Deny($"Policy {policyName} not found");

        var input = BuildPolicyInput(context);

        foreach (var rule in policy.Rules.OrderByDescending(r => r.Priority))
        {
            bool matches = true;
            foreach (var cond in rule.Conditions)
            {
                if (!PolicyEvaluator.EvaluateCondition(cond, input))
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
            {
                return rule.Effect switch
                {
                    PolicyType.Allow => PolicyDecision.Allow(rule.Reason ?? "Rule allowed"),
                    PolicyType.Hitl => PolicyDecision.Hitl(rule.Reason ?? "Rule requires HITL"),
                    _ => PolicyDecision.Deny(rule.Reason ?? "Rule denied")
                };
            }
        }
        return policy.Default switch
        {
            PolicyType.Allow => PolicyDecision.Allow(),
            PolicyType.Hitl => PolicyDecision.Hitl(),
            _ => PolicyDecision.Deny()
        };
    }

    private static Dictionary<string, object> BuildPolicyInput(PolicyEvaluationContext context)
        => new()
        {
            ["method"] = context.RequestContext.Method,
            ["path"] = context.RequestContext.Path,
            ["headers"] = context.RequestContext.Headers,
            ["agent"] = new Dictionary<string, object>
            {
                ["id"] = context.RequestContext.AgentId,
                ["trust_score"] = context.Risk.TrustScore
            },
            ["intent"] = new Dictionary<string, object>
            {
                ["label"] = context.Intent.Label,
                ["original_label"] = context.Intent.OriginalLabel ?? context.Intent.Label,
                ["confidence"] = context.Intent.Confidence,
                ["status"] = context.Intent.Status.ToString()
            },
            ["risk"] = new Dictionary<string, object>
            {
                ["score"] = context.Risk.RiskScore,
                ["level"] = context.Risk.RiskLevel
            }
        };

    private async Task<PolicyDefinition?> GetPolicyAsync(string policyName, CancellationToken cancellationToken)
    {
        var allPolicies = await GetAllPoliciesAsync(cancellationToken);
        return allPolicies.TryGetValue(policyName, out var policy) ? policy : null;
    }

    private async Task<Dictionary<string, PolicyDefinition>> GetAllPoliciesAsync(CancellationToken cancellationToken)
    {
        var (success, policies) = await _cacheProvider.TryGetValueAsync<Dictionary<string, PolicyDefinition>>(CacheKey);
        if (success && policies != null)
            return policies;

        policies = await _loader.LoadAllAsync(cancellationToken);
        await _cacheProvider.SetAsync(CacheKey, policies);
        return policies;
    }
}