namespace Vectra.Core.Entities;

public class Agent
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string OwnerId { get; private set; }
    public AgentStatus Status { get; private set; }
    public string ClientSecretHash { get; private set; }
    public double TrustScore { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Agent() { } // EF Core

    public Agent(string name, string ownerId, string clientSecretHash)
    {
        Id = Guid.NewGuid();
        Name = name;
        OwnerId = ownerId;
        Status = AgentStatus.Active;
        ClientSecretHash = clientSecretHash;
        TrustScore = 0.5;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateTrustScore(double newScore)
    {
        TrustScore = Math.Clamp(newScore, 0, 1);
    }

    public void Revoke() => Status = AgentStatus.Revoked;
}