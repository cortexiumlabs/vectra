namespace Vectra.Application.Abstractions.Executions;

public interface IPolicyEngine
{
    Task<PolicyDecision> EvaluateAsync(Guid policyId, Dictionary<string, object> input, Dictionary<string, object>? data = null);
}

public record PolicyDecision
{
    public string Effect { get; init; } = "deny";
    public string? Reason { get; init; }

    private PolicyDecision() { }

    public static PolicyDecision Allow(string? reason = null) =>
        new() { Effect = "allow", Reason = reason };

    public static PolicyDecision Deny(string? reason = null) =>
        new() { Effect = "deny", Reason = reason };

    public static PolicyDecision Hitl(string? reason = null) =>
        new() { Effect = "hitl", Reason = reason };

    public bool IsAllowed => Effect == "allow";
    public bool IsDenied => Effect == "deny";
    public bool IsHitl => Effect == "hitl";
}