namespace Vectra.BuildingBlocks.Configuration.System.CircuitBreaker;

public class CircuitBreakerConfiguration
{
    public bool Enabled { get; set; } = true;
    public int FailureThreshold { get; set; } = 5;
    public int OpenDurationSeconds { get; set; } = 30;
    public int SamplingWindowSeconds { get; set; } = 60;
}
