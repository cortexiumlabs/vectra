using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Abstractions.Security;
using Synentra.Application.Errors;
using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Results;
using Synentra.Domain.Agents;

namespace Synentra.Application.Features.Authentications.GenerateToken;

internal class GenerateTokenHandler : IActionHandler<GenerateTokenRequest, Result<GenerateTokenResult>>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ITokenService _tokenService;
    private readonly ISecretHasher _secretHasher;

    public GenerateTokenHandler(
        IAgentRepository agentRepository,
        ITokenService tokenService,
        ISecretHasher secretHasher)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _secretHasher = secretHasher ?? throw new ArgumentNullException(nameof(secretHasher));
    }

    public async Task<Result<GenerateTokenResult>> Handle(GenerateTokenRequest request, CancellationToken cancellationToken = default)
    {
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);
        if (agent == null || agent.Status != AgentStatus.Active)
            return await Result<GenerateTokenResult>.FailureAsync(Error.NotFound(
                ApplicationErrorCodes.AgentNotFound, "Agent not found or inactive"));

        if (!_secretHasher.Verify(request.ClientSecret, agent.ClientSecretHash))
            return await Result<GenerateTokenResult>.FailureAsync(Error.Unauthorized(
                ApplicationErrorCodes.InvalidClientSession, "Invalid client secret"));

        var token = _tokenService.GenerateToken(agent);
        return await Result<GenerateTokenResult>.SuccessAsync(new GenerateTokenResult { AccessToken = token });
    }
}