using Microsoft.Extensions.Logging;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Security;
using Synentra.Application.Models;
using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Results;
using System.Text;

namespace Synentra.Application.Features.Simulations.SimulateDecision;

public sealed class SimulateDecisionHandler
    : IActionHandler<SimulateDecisionRequest, Result<SimulateDecisionResult>>
{
    private readonly IDecisionEngine _decisionEngine;
    private readonly IAgentRequestAccessService _accessService;
    private readonly ILogger<SimulateDecisionHandler> _logger;

    public SimulateDecisionHandler(
        IDecisionEngine decisionEngine,
        IAgentRequestAccessService accessService,
        ILogger<SimulateDecisionHandler> logger)
    {
        _decisionEngine = decisionEngine ?? throw new ArgumentNullException(nameof(decisionEngine));
        _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<SimulateDecisionResult>> Handle(
        SimulateDecisionRequest action,
        CancellationToken cancellationToken = default)
    {
        if (action.AgentId is null)
            return Error.Unauthorized(SynentraErrors.MissingCredentials, "Missing agent identity");

        var access = await _accessService.GetAgentAsync(action.AgentId.Value, cancellationToken);
        if (!access.IsAllowed || access.Agent is null)
            return Error.Forbidden(SynentraErrors.AccessDenied, access.ForbiddenReason ?? "Access denied");

        var agent = access.Agent;

        var context = new RequestContext
        {
            Method = action.Method,
            Path = action.Path,
            TargetUrl = action.TargetUrl ?? action.Path,
            Headers = action.Headers ?? new Dictionary<string, string>(),
            AgentId = action.AgentId.Value,
            PolicyName = action.PolicyName ?? agent.PolicyName,
            TrustScore = agent.TrustScore,
            Body = action.Body
        };

        var semanticInput = BuildSemanticInput(context);

        var decision = await _decisionEngine.SimulateAsync(semanticInput, context, cancellationToken);

        return Result<SimulateDecisionResult>.Success(new SimulateDecisionResult(
            decision.Type,
            decision.Reason,
            decision.TrustScore,
            context.PolicyName));
    }

    private string BuildSemanticInput(RequestContext ctx)
    {
        // Limit total length to ~600 chars to stay well within 64 tokens (~4 chars/token average)
        var sb = new StringBuilder();

        // 1. METHOD + PATH (Highest signal for reads/writes)
        sb.Append($"{ctx.Method} {ctx.Path} ");
        // Example: "GET /todos/1 " or "POST /users "

        // 2. SELECTED HEADERS (Skip Authorization to avoid token bloat & noise)
        if (ctx.Headers.TryGetValue("Content-Type", out var ct))
            sb.Append($"Content-Type: {ct} ");
        if (ctx.Headers.TryGetValue("User-Agent", out var ua))
            sb.Append($"User-Agent: {ua} ");

        // 3. BODY (Already transformed by JsonToIntentText -> just key-value pairs)
        if (!string.IsNullOrEmpty(ctx.Body))
            sb.Append($"Body: {ctx.Body}");

        // 4. Truncate hard if somehow too long (safety net)
        var result = sb.ToString();
        return result.Length > 512 ? result.Substring(0, 512) : result;
    }
}
