using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Features.Agents.AgentsList;
using Synentra.Application.Features.Agents.AssignPolicy;
using Synentra.Application.Features.Agents.DeleteAgent;
using Synentra.Application.Features.Agents.RegisterAgent;
using Synentra.Application.Features.Authentications.GenerateToken;
using Synentra.Application.Features.Policies.PoliciesList;
using Synentra.Application.Features.Policies.PolicyDetails;
using Synentra.BuildingBlocks.Results;
using VoidType = Synentra.Application.Abstractions.Dispatchers.Void;

namespace Synentra.Application.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddSynentraApplication_ShouldRegisterAllHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSynentraApplication();

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IActionHandler<AgentsListRequest, PaginatedResult<AgentsListResult>>));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IActionHandler<CreateAgentRequest, Result<CreateAgentResult>>));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IActionHandler<AssignPolicyRequest, Result<VoidType>>));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IActionHandler<DeleteAgentRequest, Result<VoidType>>));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IActionHandler<GenerateTokenRequest, Result<GenerateTokenResult>>));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IActionHandler<PoliciesListRequest, PaginatedResult<PoliciesListResult>>));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IActionHandler<PolicyDetailsRequest, Result<PolicyDetailsResult>>));
    }

    [Fact]
    public void AddSynentraApplication_ShouldReturnSameServiceCollection()
    {
        var services = new ServiceCollection();
        var returned = services.AddSynentraApplication();

        returned.Should().BeSameAs(services);
    }
}
