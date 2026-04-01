using Vectra.Core.DTOs;
using Vectra.Core.Entities;
using Vectra.Core.Interfaces;

namespace Vectra.Core.UseCases;

public class AuthenticateAgentUseCase
{
    private readonly IAgentRepository _agentRepository;
    private readonly ITokenService _tokenService;

    public AuthenticateAgentUseCase(IAgentRepository agentRepository, ITokenService tokenService)
    {
        _agentRepository = agentRepository;
        _tokenService = tokenService;
    }

    public async Task<string?> ExecuteAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);
        if (agent == null || agent.Status != AgentStatus.Active)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(request.ClientSecret, agent.ClientSecretHash))
            return null;

        return _tokenService.GenerateToken(agent);
    }
}