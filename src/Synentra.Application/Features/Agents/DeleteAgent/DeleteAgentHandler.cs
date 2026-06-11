using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Persistence;
using Synentra.BuildingBlocks.Results;
using Void = Synentra.Application.Abstractions.Dispatchers.Void;

namespace Synentra.Application.Features.Agents.DeleteAgent;

internal class DeleteAgentHandler : IActionHandler<DeleteAgentRequest, Result<Void>>
{
    private readonly IAgentRepository _agentRepository;

    public DeleteAgentHandler(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
    }

    public async Task<Result<Void>> Handle(DeleteAgentRequest request, CancellationToken cancellationToken = default)
    {
        var agentId = Guid.Parse(request.AgentId);
        await _agentRepository.DeleteAsync(agentId, cancellationToken);
        return await Result<Void>.SuccessAsync(new Void());
    }
}