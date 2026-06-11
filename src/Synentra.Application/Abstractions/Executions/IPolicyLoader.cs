using Synentra.Domain.Policies;

namespace Synentra.Application.Abstractions.Executions;

public interface IPolicyLoader
{
    Task<PolicyDefinition?> GetPolicyAsync(string policyName, CancellationToken ct = default);
    Task<Dictionary<string, PolicyDefinition>> LoadAllAsync(CancellationToken ct = default);
}