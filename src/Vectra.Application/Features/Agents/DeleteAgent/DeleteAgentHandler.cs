using Microsoft.Extensions.Logging;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Persistence;
using Vectra.BuildingBlocks.Results;
using Void = Vectra.Application.Abstractions.Dispatchers.Void;

namespace Vectra.Application.Features.Agents.DeleteAgent;

internal class DeleteAgentHandler : IActionHandler<DeleteAgentRequest, Result<Void>>
{
    private readonly ILogger<DeleteAgentHandler> _logger;
    private readonly IAgentRepository _agentRepository;

    public DeleteAgentHandler(
        ILogger<DeleteAgentHandler> logger,
        IAgentRepository agentRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
    }

    public async Task<Result<Void>> Handle(DeleteAgentRequest request, CancellationToken cancellationToken)
    {
        var agentId = Guid.Parse(request.AgentId);
        await _agentRepository.DeleteAsync(agentId, cancellationToken);
        return Result<Void>.Success(new Void());
    }
}