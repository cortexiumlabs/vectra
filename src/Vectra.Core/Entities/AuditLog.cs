namespace Vectra.Core.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string Action { get; private set; } // e.g., "POST /users"
    public string TargetUrl { get; private set; }
    public string Status { get; private set; } // ALLOWED, DENIED, PENDING_HITL
    public int? HttpStatusCode { get; private set; }
    public double RiskScore { get; private set; }
    public string Intent { get; private set; }
    public DateTime Timestamp { get; private set; }

    private AuditLog() { }

    public AuditLog(Guid agentId, string action, string targetUrl, string status, double riskScore, string intent, int? httpStatusCode = null)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
        Action = action;
        TargetUrl = targetUrl;
        Status = status;
        HttpStatusCode = httpStatusCode;
        RiskScore = riskScore;
        Intent = intent;
        Timestamp = DateTime.UtcNow;
    }
}