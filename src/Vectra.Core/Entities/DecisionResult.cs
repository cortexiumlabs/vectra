namespace Vectra.Core.Entities;

public record DecisionResult
{
    public DecisionType Type { get; init; }
    public string? Reason { get; init; }
    public bool IsAllowed => Type == DecisionType.Allow;
    public bool IsHitl => Type == DecisionType.Hitl;
    public bool IsDenied => Type == DecisionType.Deny;

    public static DecisionResult Allow() => new DecisionResult { Type = DecisionType.Allow };
    public static DecisionResult Deny(string reason) => new DecisionResult { Type = DecisionType.Deny, Reason = reason };
    public static DecisionResult Hitl(string reason) => new DecisionResult { Type = DecisionType.Hitl, Reason = reason };
}