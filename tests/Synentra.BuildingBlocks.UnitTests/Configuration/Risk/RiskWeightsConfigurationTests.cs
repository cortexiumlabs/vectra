using FluentAssertions;
using Synentra.BuildingBlocks.Configuration.Risk;
using Xunit;

namespace Synentra.BuildingBlocks.UnitTests.Configuration.Risk;

public class RiskWeightsConfigurationTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new RiskWeightsConfiguration();

        config.MethodRisk.Should().Be(0.15);
        config.PathRisk.Should().Be(0.20);
        config.BodySizeRisk.Should().Be(0.05);
        config.TimeBasedRisk.Should().Be(0.05);
        config.AgentHistoryRisk.Should().Be(0.15);
        config.AnomalyDetectionRisk.Should().Be(0.15);
        config.IntentRisk.Should().Be(0.25);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var config = new RiskWeightsConfiguration
        {
            MethodRisk = 0.1,
            PathRisk = 0.2,
            BodySizeRisk = 0.3,
            TimeBasedRisk = 0.4,
            AgentHistoryRisk = 0.5,
            AnomalyDetectionRisk = 0.6,
            IntentRisk = 0.7
        };

        config.MethodRisk.Should().Be(0.1);
        config.PathRisk.Should().Be(0.2);
        config.BodySizeRisk.Should().Be(0.3);
        config.TimeBasedRisk.Should().Be(0.4);
        config.AgentHistoryRisk.Should().Be(0.5);
        config.AnomalyDetectionRisk.Should().Be(0.6);
        config.IntentRisk.Should().Be(0.7);
    }

    [Theory]
    [InlineData("MethodRisk")]
    [InlineData("PathRisk")]
    [InlineData("BodySizeRisk")]
    [InlineData("TimeBasedRisk")]
    [InlineData("AgentHistoryRisk")]
    [InlineData("AnomalyDetectionRisk")]
    [InlineData("IntentRisk")]
    public void GetWeight_ForKnownCalculatorName_ReturnsCorrespondingPropertyValue(string calculatorName)
    {
        var config = new RiskWeightsConfiguration();
        // Set each property to a distinct value so we can assert mapping correctness
        SetProperty(config, calculatorName, 0.99);

        double? result = config.GetWeight(calculatorName);

        result.Should().Be(0.99);
    }

    [Theory]
    [InlineData("NonExistent")]
    [InlineData("")]
    [InlineData(null)]
    public void GetWeight_ForUnknownCalculatorName_ReturnsNull(string calculatorName)
    {
        var config = new RiskWeightsConfiguration();

        double? result = config.GetWeight(calculatorName!);

        result.Should().BeNull();
    }

    private static void SetProperty(RiskWeightsConfiguration config, string propertyName, double value)
    {
        switch (propertyName)
        {
            case "MethodRisk": config.MethodRisk = value; break;
            case "PathRisk": config.PathRisk = value; break;
            case "BodySizeRisk": config.BodySizeRisk = value; break;
            case "TimeBasedRisk": config.TimeBasedRisk = value; break;
            case "AgentHistoryRisk": config.AgentHistoryRisk = value; break;
            case "AnomalyDetectionRisk": config.AnomalyDetectionRisk = value; break;
            case "IntentRisk": config.IntentRisk = value; break;
        }
    }
}