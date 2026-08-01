namespace Synentra.BuildingBlocks.Configuration.Risk;

public sealed class RiskWeightsConfiguration
{
    public double MethodRisk { get; set; } = 0.15;
    public double PathRisk { get; set; } = 0.20;
    public double BodySizeRisk { get; set; } = 0.05;
    public double TimeBasedRisk { get; set; } = 0.05;
    public double AgentHistoryRisk { get; set; } = 0.15;
    public double AnomalyDetectionRisk { get; set; } = 0.15;
    public double IntentRisk { get; set; } = 0.25;

    public double? GetWeight(string calculatorName)
        => calculatorName switch
        {
            "MethodRisk" => MethodRisk,
            "PathRisk" => PathRisk,
            "BodySizeRisk" => BodySizeRisk,
            "TimeBasedRisk" => TimeBasedRisk,
            "AgentHistoryRisk" => AgentHistoryRisk,
            "AnomalyDetectionRisk" => AnomalyDetectionRisk,
            "IntentRisk" => IntentRisk,
            _ => null
        };
}
