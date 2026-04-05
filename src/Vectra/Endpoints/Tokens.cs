using Microsoft.AspNetCore.Mvc;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Features.Authentications.GenerateToken;
using Vectra.Extensions;
using Vectra.Application.Extensions;

namespace Vectra.Endpoints;

public class Tokens : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Authentication");

        group.MapPost("", GetToken)
            .WithName("GetToken")
            .WithSummary("Exchange credentials for JWT");
    }

    public static async Task<IResult> GetToken(
        [FromBody] GenerateTokenRequest request,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.GenerateToken(request, cancellationToken);
        return result.ToHttpResult();
    }
}