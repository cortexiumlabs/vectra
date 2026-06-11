using Vectra.Domain.Policies;

namespace Vectra.Application.Features.Policies.PolicyDetails;

public class PolicyDetailsResult
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Owner { get; set; } = string.Empty;
    public DateTime? CreatedOn { get; set; }
    public PolicyType Default { get; set; }
    public List<PolicyRule> Rules { get; set; } = [];
}
