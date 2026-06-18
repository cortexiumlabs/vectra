using Microsoft.AspNetCore.Mvc;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Features.Authentications.GenerateToken;
using Synentra.Extensions;
using Synentra.Application.Extensions;

namespace Synentra.Endpoints;

public class Tokens : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Authentication");

        group.MapPost("", GetToken)
            .WithName("GetToken")
            .WithSummary("Exchange credentials for gateway JWT used in Synentra-Authorization header");
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