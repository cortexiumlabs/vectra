using Microsoft.Extensions.Logging;
using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Errors;
using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Agents.AssignPolicy;

internal class AssignPolicyHandler : IActionHandler<AssignPolicyRequest, Result<Abstractions.Dispatchers.Void>>
{
    private readonly ILogger<AssignPolicyHandler> _logger;
    private readonly IAgentRepository _agentRepository;
    private readonly IPolicyLoader _policyLoader;

    public AssignPolicyHandler(
        ILogger<AssignPolicyHandler> logger,
        IAgentRepository agentRepository,
        IPolicyLoader policyLoader)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _policyLoader = policyLoader ?? throw new ArgumentNullException(nameof(policyLoader));
    }

    public async Task<Result<Abstractions.Dispatchers.Void>> Handle(AssignPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var agentId = Guid.Parse(request.AgentId);
        var agent = await _agentRepository.GetByIdAsync(agentId, cancellationToken);
        if (agent == null)
        {
            _logger.LogWarning("Agent with ID {AgentId} not found.", request.AgentId);
            var error = Error.NotFound(ApplicationErrorCodes.AgentNotFound, $"Agent with ID {request.AgentId} not found.");
            return await Result<Abstractions.Dispatchers.Void>.FailureAsync(error);
        }

        var policyName = request.PolicyName;
        var policy = await _policyLoader.GetPolicyAsync(policyName, cancellationToken);
        if (policy == null)
        {
            _logger.LogWarning("Policy with name {PolicyName} not found.", request.PolicyName);
            var error = Error.NotFound(ApplicationErrorCodes.PolicyNotFound, $"Policy with name {request.PolicyName} not found.");
            return await Result<Abstractions.Dispatchers.Void>.FailureAsync(error);
        }

        agent.PolicyName = policyName;
        await _agentRepository.UpdateAsync(agent, cancellationToken);
        return await Result<Abstractions.Dispatchers.Void>.SuccessAsync(new Abstractions.Dispatchers.Void());
    }
}
