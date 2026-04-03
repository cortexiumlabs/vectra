using Microsoft.AspNetCore.Mvc;
using Vectra.Core.DTOs;
using Vectra.Core.UseCases;
using Vectra.Extensions;

namespace Vectra.Endpoints;

public class Agents : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Agents");

        group.MapPost("", RegisterAgent)
            .WithName("RegisterAgent")
            .WithSummary("Register a new AI agent")
            .Produces<RegisterResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }

    public static async Task<IResult> RegisterAgent(
        [FromBody] RegisterAgentRequest request,
        RegisterAgentUseCase useCase,
        CancellationToken cancellationToken)
    {
        var agentId = await useCase.ExecuteAsync(request, cancellationToken);
        return Results.Ok(new { agent_id = agentId });
    }

    public record RegisterResponse(Guid agent_id);
}