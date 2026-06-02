namespace Vectra.BuildingBlocks.Configuration.Security.AgentQuarantine;

public class AgentQuarantineConfiguration
{
    public bool? Enabled { get; set; } = true;

    /// <summary>
    /// If an agent's trust score is below this floor, it is automatically quarantined.
    /// </summary>
    public double TrustScoreFloor { get; set; } = 0.3;
}