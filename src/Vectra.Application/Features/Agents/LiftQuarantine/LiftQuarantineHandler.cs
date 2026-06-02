using Microsoft.Extensions.Logging;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Persistence;
using Vectra.Application.Errors;
using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Results;
using Void = Vectra.Application.Abstractions.Dispatchers.Void;

namespace Vectra.Application.Features.Agents.LiftQuarantine;

internal class LiftQuarantineHandler : IActionHandler<LiftQuarantineRequest, Result<Void>>
{
    private readonly ILogger<LiftQuarantineHandler> _logger;
    private readonly IAgentRepository _agentRepository;

    public LiftQuarantineHandler(
        ILogger<LiftQuarantineHandler> logger,
        IAgentRepository agentRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
    }

    public async Task<Result<Void>> Handle(LiftQuarantineRequest request, CancellationToken cancellationToken = default)
    {
        var agentId = Guid.Parse(request.AgentId);
        var agent = await _agentRepository.GetByIdAsync(agentId, cancellationToken);
        if (agent == null)
        {
            _logger.LogWarning("Agent with ID {AgentId} not found.", request.AgentId);
            var error = Error.NotFound(ApplicationErrorCodes.AgentNotFound, $"Agent with ID {request.AgentId} not found.");
            return await Result<Void>.FailureAsync(error);
        }

        agent.LiftQuarantine();
        await _agentRepository.UpdateAsync(agent, cancellationToken);

        return await Result<Void>.SuccessAsync(new Void());
    }
}
