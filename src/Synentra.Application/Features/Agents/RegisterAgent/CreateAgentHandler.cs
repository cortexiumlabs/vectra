using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Persistence;
using Vectra.Application.Abstractions.Security;
using Vectra.BuildingBlocks.Results;
using Vectra.Domain.Agents;

namespace Vectra.Application.Features.Agents.RegisterAgent;

internal class CreateAgentHandler : IActionHandler<CreateAgentRequest, Result<CreateAgentResult>>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ISecretHasher _secretHasher;

    public CreateAgentHandler(
        IAgentRepository agentRepository,
        ISecretHasher secretHasher)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _secretHasher = secretHasher ?? throw new ArgumentNullException(nameof(secretHasher));
    }

    public async Task<Result<CreateAgentResult>> Handle(CreateAgentRequest request, CancellationToken cancellationToken = default)
    {
        var clientSecretHash = _secretHasher.HashPassword(request.ClientSecret);
        var agent = new Agent(request.Name, request.OwnerId, clientSecretHash);
        await _agentRepository.AddAsync(agent, cancellationToken);

        return await Result<CreateAgentResult>.SuccessAsync(new CreateAgentResult { AgentId = agent.Id });
    }
}