using Vectra.Domain.Primitives;

namespace Vectra.Domain.Policies;

public class PolicyDefinition : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PolicyRule> Rules { get; set; } = new List<PolicyRule>();
}