using Vectra.Domain.Primitives;

namespace Vectra.Domain.Agents;

public class AgentHistory: AuditableEntity<Guid>
{
    public Guid AgentId { get; set; }
    public DateTime WindowStart { get; set; }      // start of the aggregation window (e.g., minute)
    public int WindowDurationSeconds { get; set; } = 60; // default 1 minute
    public int TotalRequests { get; set; }
    public int ViolationCount { get; set; }        // denied or HITL requests
    public double AverageRiskScore { get; set; }

    // Navigation property
    public Agent Agent { get; set; } = null!;
}