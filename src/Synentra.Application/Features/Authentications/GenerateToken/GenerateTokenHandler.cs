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
    private readonly IAgentAuthConfigProvider _authConfig;

    public GenerateTokenHandler(
        IAgentRepository agentRepository,
        IAgentAuthenticator agentAuthenticator,
        ISecretHasher secretHasher,
        IAgentAuthConfigProvider authConfig)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _agentAuthenticator = agentAuthenticator ?? throw new ArgumentNullException(nameof(agentAuthenticator));
        _secretHasher = secretHasher ?? throw new ArgumentNullException(nameof(secretHasher));
        _authConfig = authConfig ?? throw new ArgumentNullException(nameof(authConfig));
    }

    public async Task<Result<GenerateTokenResult>> Handle(GenerateTokenRequest request, CancellationToken cancellationToken = default)
    {
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);
        if (agent == null || agent.Status != AgentStatus.Active)
            return await Result<GenerateTokenResult>.FailureAsync(Error.NotFound(
                ApplicationErrorCodes.AgentNotFound, "Agent not found or inactive"));

        bool isAuthenticated = false;

        // 1. Try external token if provided (Jwt flow)
        if (!string.IsNullOrWhiteSpace(request.ExternalToken))
        {
            var principal = await _agentAuthenticator.ValidateAsync(request.ExternalToken, cancellationToken);
            if (principal != null)
            {
                isAuthenticated = true;
            }
            else
            {
                return await Result<GenerateTokenResult>.FailureAsync(Error.Unauthorized(
                    ApplicationErrorCodes.InvalidClientSession,
                    "Invalid external token."));
            }
        }

        // 2. Fallback to client_secret (SelfSigned flow)
        if (!isAuthenticated && !string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            if (_secretHasher.Verify(request.ClientSecret, agent.ClientSecretHash))
                isAuthenticated = true;
            else
                return await Result<GenerateTokenResult>.FailureAsync(Error.Unauthorized(
                    ApplicationErrorCodes.InvalidClientSession, "Invalid client secret."));
        }

        if (!isAuthenticated)
        {
            return await Result<GenerateTokenResult>.FailureAsync(Error.Unauthorized(
                ApplicationErrorCodes.InvalidClientSession,
                "Either ClientSecret or ExternalToken must be provided and valid."));
        }

        var authResult = _agentAuthenticator.Authenticate(agent);
        if (!authResult.Succeeded || string.IsNullOrWhiteSpace(authResult.Token))
            return await Result<GenerateTokenResult>.FailureAsync(Error.Unauthorized(
                ApplicationErrorCodes.InvalidClientSession,
                authResult.Error ?? "Token generation failed."));

        return await Result<GenerateTokenResult>.SuccessAsync(new GenerateTokenResult { AccessToken = authResult.Token });
    }
}