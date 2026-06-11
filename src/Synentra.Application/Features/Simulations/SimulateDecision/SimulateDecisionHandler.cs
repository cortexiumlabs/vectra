using Microsoft.Extensions.Logging;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Executions;
using Vectra.Application.Abstractions.Security;
using Vectra.Application.Models;
using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Simulations.SimulateDecision;

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
            return Error.Unauthorized(VectraErrors.MissingCredentials, "Missing agent identity");

        var access = await _accessService.GetAgentAsync(action.AgentId.Value, cancellationToken);
        if (!access.IsAllowed || access.Agent is null)
            return Error.Forbidden(VectraErrors.AccessDenied, access.ForbiddenReason ?? "Access denied");

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

        var decision = await _decisionEngine.SimulateAsync(context, cancellationToken);

        return Result<SimulateDecisionResult>.Success(new SimulateDecisionResult(
            decision.Type,
            decision.Reason,
            decision.TrustScore,
            context.PolicyName));
    }
}
