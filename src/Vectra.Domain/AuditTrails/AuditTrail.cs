using Vectra.Domain.Primitives;

namespace Vectra.Domain.AuditTrails;

public class AuditTrail : Entity<long>, IAggregateRoot
{
    public Guid AgentId { get; set; }
    public string Action { get; set; } = string.Empty; // e.g., "POST /users"
    public string TargetUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // ALLOWED, DENIED, PENDING_HITL
    public double? RiskScore { get; set; }
    public string? Intent { get; set; }
    public string? Reason { get; set; }
    public DateTime? Timestamp { get; set; }
}