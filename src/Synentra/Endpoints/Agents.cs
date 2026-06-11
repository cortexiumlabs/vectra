using Microsoft.AspNetCore.Mvc;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Extensions;
using Synentra.Application.Features.Agents.AssignPolicy;
using Synentra.Application.Features.Agents.RegisterAgent;
using Synentra.Extensions;

namespace Synentra.Endpoints;

public class Agents : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Agents");

        group.MapGet("", AgentsList)
            .WithName("AgentsList")
            .WithSummary("Get a list of AI agents");

        group.MapPost("", RegisterAgent)
            .WithName("RegisterAgent")
            .WithSummary("Register a new AI agent");

        group.MapPut("/{agentId}/policy", AssignPolicyToAgent)
            .WithName("AssignPolicyToAgent")
            .WithSummary("Assign a policy to an AI agent");

        group.MapPost("/{agentId}/lift-quarantine", LiftAgentQuarantine)
            .WithName("LiftAgentQuarantine")
            .WithSummary("Lift quarantine mode for an AI agent");

        group.MapDelete("/{agentId}", DeleteAgent)
            .WithName("DeleteAgent")
            .WithSummary("Delete an AI agent");
    }

    public static async Task<IResult> AgentsList(
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await dispatcher.AgentsList(page, pageSize, cancellationToken);
        return result.ToHttpResult();
    }

    public static async Task<IResult> RegisterAgent(
        [FromBody] CreateAgentRequest request,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.RegisterAgent(request, cancellationToken);
        return result.ToHttpResult();
    }

    public static async Task<IResult> AssignPolicyToAgent(
        string agentId,
        [FromBody] AssignPolicyRequestModel request,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.AssignPolicyToAgent(agentId, request.PolicyName, cancellationToken);
        return result.ToHttpResult();
    }

    public static async Task<IResult> DeleteAgent(
        string agentId,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.DeleteAgent(Guid.Parse(agentId), cancellationToken);
        return result.ToHttpResult();
    }

    public static async Task<IResult> LiftAgentQuarantine(
        string agentId,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.LiftAgentQuarantine(agentId, cancellationToken);
        return result.ToHttpResult();
    }
}