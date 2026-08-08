using FluentAssertions;
using Synentra.BuildingBlocks.Configuration.HumanInTheLoop;
using Xunit;

namespace Synentra.BuildingBlocks.UnitTests.Configuration.HumanInTheLoop;

public class HumanInTheLoopConfigurationTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new HumanInTheLoopConfiguration();

        config.Enabled.Should().BeTrue();
        config.TimeoutSeconds.Should().Be(3600);
        config.MaxPendingRequests.Should().Be(100);
    }

    [Fact]
    public void ShouldAllowCustomValues()
    {
        var config = new HumanInTheLoopConfiguration
        {
            Enabled = false,
            TimeoutSeconds = 7200,
            MaxPendingRequests = 200
        };

        config.Enabled.Should().BeFalse();
        config.TimeoutSeconds.Should().Be(7200);
        config.MaxPendingRequests.Should().Be(200);
    }

    [Fact]
    public void MaxPendingRequests_Zero_ShouldMeanUnlimited()
    {
        var config = new HumanInTheLoopConfiguration { MaxPendingRequests = 0 };

        config.MaxPendingRequests.Should().Be(0);
    }
}
