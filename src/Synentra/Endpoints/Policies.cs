using Microsoft.AspNetCore.Mvc;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Extensions;
using Vectra.Extensions;

namespace Vectra.Endpoints;

public class Policies : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Policies");

        group.MapGet("", PoliciesList)
            .WithName("PoliciesList")
            .WithSummary("Get a list of AI policies");

        group.MapGet("/{name}", PolicyDetails)
            .WithName("PolicyDetails")
            .WithSummary("Get details of a specific policy");
    }

    public static async Task<IResult> PoliciesList(
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await dispatcher.PoliciesList(page, pageSize, cancellationToken);
        return result.ToHttpResult();
    }

    public static async Task<IResult> PolicyDetails(
        string name,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.PolicyDetails(name, cancellationToken);
        return result.ToHttpResult();
    }
}
