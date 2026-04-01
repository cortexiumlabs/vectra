namespace Vectra.Core.Entities;

public class Policy
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string TargetApi { get; private set; } // e.g., "api.github.com"
    public List<string> AllowedMethods { get; private set; }
    public bool RequiresHitl { get; private set; }
    public int? CostLimit { get; private set; }
    public Agent Agent { get; private set; } = null!;

    private Policy() { }

    public Policy(Guid agentId, string targetApi, List<string> allowedMethods, bool requiresHitl, int? costLimit = null)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
        TargetApi = targetApi;
        AllowedMethods = allowedMethods;
        RequiresHitl = requiresHitl;
        CostLimit = costLimit;
    }
}