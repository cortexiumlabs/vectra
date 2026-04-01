using Vectra.Core.DTOs;
using Vectra.Core.Entities;
using Vectra.Core.Interfaces;

namespace Vectra.Core.UseCases;

public class RegisterAgentUseCase
{
    private readonly IAgentRepository _agentRepository;

    public RegisterAgentUseCase(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository;
    }

    public async Task<Guid> ExecuteAsync(RegisterAgentRequest request, CancellationToken cancellationToken = default)
    {
        var clientSecretHash = BCrypt.Net.BCrypt.HashPassword(request.ClientSecret);
        var agent = new Agent(request.Name, request.OwnerId, clientSecretHash);
        await _agentRepository.AddAsync(agent, cancellationToken);
        return agent.Id;
    }
}