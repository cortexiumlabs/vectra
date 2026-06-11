using Microsoft.Extensions.Options;
using Vectra.Application.Abstractions.Persistence;
using Vectra.BuildingBlocks.Configuration.Security.AgentQuarantine;
using Vectra.BuildingBlocks.Configuration.Security;
using Vectra.Domain.Agents;
using Vectra.Infrastructure.Security;

namespace Vectra.UnitTests.Services;

public class AgentRequestAccessServiceTests
{
    private static IOptions<SecurityConfiguration> Options(SecurityConfiguration cfg)
        => Microsoft.Extensions.Options.Options.Create(cfg);

    [Fact]
    public async Task GetAgentAsync_AgentNotFound_ReturnsForbiddenNotActive()
    {
        var repo = Substitute.For<IAgentRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Agent?)null);

        var cfg = new SecurityConfiguration();
        var sut = new AgentRequestAccessService(repo, Options(cfg));

        var result = await sut.GetAgentAsync(Guid.NewGuid());

        result.IsAllowed.Should().BeFalse();
        result.Agent.Should().BeNull();
        result.ForbiddenReason.Should().Be("Agent is not active");
    }

    [Fact]
    public async Task GetAgentAsync_QuarantinedAgent_ReturnsForbiddenQuarantined()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("test", "owner", "hash");
        agent.Quarantine();

        var repo = Substitute.For<IAgentRepository>();
        repo.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);

        var cfg = new SecurityConfiguration();
        var sut = new AgentRequestAccessService(repo, Options(cfg));

        var result = await sut.GetAgentAsync(agentId);

        result.IsAllowed.Should().BeFalse();
        result.Agent.Should().Be(agent);
        result.ForbiddenReason.Should().Be("Agent is quarantined");
        await repo.DidNotReceive().UpdateAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAgentAsync_RevokedAgent_ReturnsForbiddenNotActive()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("test", "owner", "hash");
        agent.Revoke();

        var repo = Substitute.For<IAgentRepository>();
        repo.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);

        var cfg = new SecurityConfiguration();
        var sut = new AgentRequestAccessService(repo, Options(cfg));

        var result = await sut.GetAgentAsync(agentId);

        result.IsAllowed.Should().BeFalse();
        result.ForbiddenReason.Should().Be("Agent is not active");
        await repo.DidNotReceive().UpdateAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAgentAsync_TrustBelowFloor_QuarantinesAndPersists_ReturnsForbiddenQuarantined()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("test", "owner", "hash");
        agent.UpdateTrustScore(0.1);

        var repo = Substitute.For<IAgentRepository>();
        repo.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);

        var cfg = new SecurityConfiguration
        {
            AgentQuarantine = new AgentQuarantineConfiguration
            {
                Enabled = true,
                TrustScoreFloor = 0.3
            }
        };

        var sut = new AgentRequestAccessService(repo, Options(cfg));

        var result = await sut.GetAgentAsync(agentId);

        result.IsAllowed.Should().BeFalse();
        result.ForbiddenReason.Should().Be("Agent is quarantined");
        agent.Status.Should().Be(AgentStatus.Quarantined);
        await repo.Received(1).UpdateAsync(Arg.Is<Agent>(a => a.Status == AgentStatus.Quarantined), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAgentAsync_QuarantineDisabled_DoesNotQuarantine_ReturnsAllowed()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("test", "owner", "hash");
        agent.UpdateTrustScore(0.1);

        var repo = Substitute.For<IAgentRepository>();
        repo.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(agent);

        var cfg = new SecurityConfiguration
        {
            AgentQuarantine = new AgentQuarantineConfiguration
            {
                Enabled = false,
                TrustScoreFloor = 0.3
            }
        };

        var sut = new AgentRequestAccessService(repo, Options(cfg));

        var result = await sut.GetAgentAsync(agentId);

        result.IsAllowed.Should().BeTrue();
        result.Agent.Should().Be(agent);
        result.ForbiddenReason.Should().BeNull();
        agent.Status.Should().Be(AgentStatus.Active);
        await repo.DidNotReceive().UpdateAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }
}
