using Vectra.Domain.Primitives;

namespace Vectra.Domain.Policies;

public class RuleCondition : AuditableEntity<Guid>
{
    public Guid RuleId { get; set; }
    public string LogicalOperator { get; set; } = "and";   // and, or
    public int Order { get; set; } = 0;
    public string Attribute { get; set; } = string.Empty;  // e.g., "input.method", "data.user.role"
    public string Operator { get; set; } = string.Empty;   // eq, ne, gt, lt, ge, le, in, contains, startsWith, endsWith, regex
    public string? ValueJson { get; set; }                 // JSON serialized value
    public PolicyRule Rule { get; set; } = null!;
}