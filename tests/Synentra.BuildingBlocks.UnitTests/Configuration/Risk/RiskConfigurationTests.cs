using FluentAssertions;
using Synentra.BuildingBlocks.Configuration.Risk;
using Xunit;

namespace Synentra.BuildingBlocks.UnitTests.Configuration.Risk;

public class RiskConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetEnabledToTrueByDefault()
    {
        var config = new RiskConfiguration();

        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldCreateDefaultWeightsInstance()
    {
        var config = new RiskConfiguration();

        config.Weights.Should().NotBeNull();
        config.Weights.Should().BeOfType<RiskWeightsConfiguration>();
    }

    [Fact]
    public void Enabled_ShouldAllowSettingToFalse()
    {
        var config = new RiskConfiguration { Enabled = false };

        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Enabled_ShouldAllowSettingToNull()
    {
        var config = new RiskConfiguration { Enabled = null };

        config.Enabled.Should().BeNull();
    }

    [Fact]
    public void Enabled_ShouldAllowSettingToTrue()
    {
        var config = new RiskConfiguration { Enabled = false };
        config.Enabled = true;

        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Weights_ShouldAllowSettingToCustomInstance()
    {
        var customWeights = new RiskWeightsConfiguration();
        var config = new RiskConfiguration { Weights = customWeights };

        config.Weights.Should().BeSameAs(customWeights);
    }

    [Fact]
    public void Weights_ShouldBeIndependentBetweenInstances()
    {
        var config1 = new RiskConfiguration();
        var config2 = new RiskConfiguration();

        config1.Weights.Should().NotBeSameAs(config2.Weights);
    }
}