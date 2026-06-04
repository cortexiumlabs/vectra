using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Extensions;
using Vectra.Application.Features.Simulations.SimulateDecision;
using Vectra.Extensions;
using Vectra.Infrastructure.Decision;

namespace Vectra.Endpoints;

public class Simulate : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this).WithTags("Simulation");

        group.MapPost("", Run)
            .WithName("SimulatePolicy")
            .WithSummary("Simulate a decision (dry-run) without auditing or suspending");
    }

    public static async Task<IResult> Run(
        [FromBody] SimulateDecisionRequest request,
        HttpContext httpContext,
        [FromServices] IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Items.TryGetValue("AgentId", out var agentIdObj) || agentIdObj is not Guid agentId)
            return Results.Unauthorized();

        request = request with
        {
            AgentId = agentId,
            Body = ConvertBodyIfJson(request.Body, request.ContentType)
        };

        var result = await dispatcher.SimulateDecision(request, cancellationToken);
        return result.ToHttpResult();
    }

    private static string? ConvertBodyIfJson(string? body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body))
            return body;

        var isJson = contentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true
                   || contentType?.Contains("+json", StringComparison.OrdinalIgnoreCase) == true;
        if (!isJson)
            return body;

        try
        {
            return JsonToIntentText.Convert(body);
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
