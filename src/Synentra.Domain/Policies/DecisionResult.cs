namespace Vectra.Domain.Policies;

public record DecisionResult
{
    public DecisionType Type { get; init; }
    public string? Reason { get; init; }
    public double TrustScore { get; set; }
    public bool IsAllowed => Type == DecisionType.Allow;
    public bool IsHitl => Type == DecisionType.Hitl;
    public bool IsDenied => Type == DecisionType.Deny;

    public static DecisionResult Allow(double trustScore = 1.0) => new DecisionResult { Type = DecisionType.Allow, TrustScore = trustScore };
    public static DecisionResult Deny(string reason, double trustScore = 0.0) => new DecisionResult { Type = DecisionType.Deny, Reason = reason, TrustScore = trustScore };
    public static DecisionResult Hitl(string reason, double trustScore = 0.5) => new DecisionResult { Type = DecisionType.Hitl, Reason = reason, TrustScore = trustScore };
}