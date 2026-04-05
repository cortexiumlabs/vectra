using Microsoft.Extensions.Logging;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Persistence;
using Vectra.BuildingBlocks.Results;
using Vectra.Domain.Agents;

namespace Vectra.Application.Features.Agents.RegisterAgent;

internal class CreateAgentHandler : IActionHandler<CreateAgentRequest, Result<CreateAgentResult>>
{
    private readonly ILogger<CreateAgentHandler> _logger;
    private readonly IAgentRepository _agentRepository;

    public CreateAgentHandler(
        ILogger<CreateAgentHandler> logger,
        IAgentRepository agentRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
    }

    public async Task<Result<CreateAgentResult>> Handle(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        var clientSecretHash = BCrypt.Net.BCrypt.HashPassword(request.ClientSecret);
        var agent = new Agent(request.Name, request.OwnerId, clientSecretHash);
        await _agentRepository.AddAsync(agent, cancellationToken);

        return Result<CreateAgentResult>.Success(new CreateAgentResult { AgentId = agent.Id });
    }
}