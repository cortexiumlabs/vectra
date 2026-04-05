using Microsoft.AspNetCore.Mvc;
using Vectra.Application.Abstractions.Persistence;
using Vectra.Domain.Policies;
using Vectra.Extensions;

namespace Vectra.Endpoints;

public class Policies : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Policies");

        group.MapPost("/{id}", PolicyDetails)
            .WithName("GetPolicyDetails")
            .WithSummary("Get details of a specific policy")
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", CreatePolicy)
            .WithName("CreatePolicy")
            .WithSummary("Create a new policy")
            .Produces(StatusCodes.Status400BadRequest);
    }

    public static async Task<IResult> PolicyDetails(
        [FromServices] IPolicyRepository policyRepository,
        CancellationToken cancellationToken,
        [FromRoute] Guid id)
    {
        var policy = await policyRepository.GetByIdAsync(id, cancellationToken);
        return policy is null ? Results.NotFound() : Results.Ok(policy);
    }

    public static async Task<IResult> CreatePolicy(
        [FromBody] PolicyDefinition policy,
        IPolicyRepository policyRepository,
        CancellationToken cancellationToken)
    {
        await policyRepository.AddAsync(policy, cancellationToken);
        return Results.Created($"/policies/{policy.Id}", policy);
    }
}
