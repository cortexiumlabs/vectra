namespace Synentra.Application.Features.Audit.AuditList;

public class AuditListResult
{
    public long Id { get; set; }
    public Guid AgentId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? RiskScore { get; set; }
    public string? Intent { get; set; }
    public string? Reason { get; set; }
    public DateTime? Timestamp { get; set; }
}
