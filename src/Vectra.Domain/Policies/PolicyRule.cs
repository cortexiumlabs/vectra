using Vectra.Domain.Primitives;

namespace Vectra.Domain.Policies;

public class PolicyRule : AuditableEntity<Guid>
{
    public Guid PolicyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;          // higher = more important
    public string Effect { get; set; } = "deny";    // allow, deny, hitl
    public string? Reason { get; set; }
    public PolicyDefinition Policy { get; set; } = null!;
    public ICollection<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();
}
