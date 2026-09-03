using Microsoft.AspNetCore.Mvc;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Extensions;
using Synentra.Extensions;

namespace Synentra.Endpoints;

public class Audit : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Audit");

        group.MapGet("", AuditList)
            .WithName("AuditList")
            .WithSummary("Get a paginated list of audit trails");

        group.MapGet("/{id:long}", AuditDetails)
            .WithName("AuditDetails")
            .WithSummary("Get details of a specific audit trail");
    }

    public static async Task<IResult> AuditList(
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await dispatcher.AuditList(page, pageSize, cancellationToken);
        return result.ToHttpResult();
    }

    public static async Task<IResult> AuditDetails(
        long id,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.AuditDetails(id, cancellationToken);
        return result.ToHttpResult();
    }
}
