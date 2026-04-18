using FluentAssertions;
using Vectra.Domain.Agents;

namespace Vectra.Domain.UnitTests.Agents;

public class AgentTests
{
    [Fact]
    public void Constructor_ShouldInitializeAgentWithCorrectValues()
    {
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");

        agent.Name.Should().Be("TestAgent");
        agent.OwnerId.Should().Be("owner-1");
        agent.ClientSecretHash.Should().Be("hashed-secret");
        agent.Status.Should().Be(AgentStatus.Active);
        agent.TrustScore.Should().Be(0.5);
        agent.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        var agent1 = new Agent("Agent1", "owner-1", "hash1");
        var agent2 = new Agent("Agent2", "owner-2", "hash2");

        agent1.Id.Should().NotBe(agent2.Id);
    }

    [Fact]
    public void UpdateTrustScore_ShouldSetScoreWithinRange()
    {
        var agent = new Agent("TestAgent", "owner-1", "hash");

        agent.UpdateTrustScore(0.8);

        agent.TrustScore.Should().Be(0.8);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(1.5, 1.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void UpdateTrustScore_ShouldClampValueBetweenZeroAndOne(double input, double expected)
    {
        var agent = new Agent("TestAgent", "owner-1", "hash");

        agent.UpdateTrustScore(input);

        agent.TrustScore.Should().Be(expected);
    }

    [Fact]
    public void Revoke_ShouldSetStatusToRevoked()
    {
        var agent = new Agent("TestAgent", "owner-1", "hash");

        agent.Revoke();

        agent.Status.Should().Be(AgentStatus.Revoked);
    }

    [Fact]
    public void AgentHistories_ShouldBeEmptyByDefault()
    {
        var agent = new Agent("TestAgent", "owner-1", "hash");

        agent.AgentHistories.Should().BeEmpty();
    }
}
