using Synentra.Application.Abstractions.Dispatchers;
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
    private readonly IAgentAuthenticator _agentAuthenticator;
    private readonly ISecretHasher _secretHasher;

    public GenerateTokenHandler(
        IAgentRepository agentRepository,
        IAgentAuthenticator agentAuthenticator,
        ISecretHasher secretHasher)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _agentAuthenticator = agentAuthenticator ?? throw new ArgumentNullException(nameof(agentAuthenticator));
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

        var authResult = _agentAuthenticator.Authenticate(agent);
        if (!authResult.Succeeded || string.IsNullOrWhiteSpace(authResult.Token))
            return await Result<GenerateTokenResult>.FailureAsync(Error.Unauthorized(
                ApplicationErrorCodes.InvalidClientSession,
                authResult.Error ?? "Token generation is not available for the configured authentication provider"));

        var token = authResult.Token;
        return await Result<GenerateTokenResult>.SuccessAsync(new GenerateTokenResult { AccessToken = token });
    }
}