using System.Security.Claims;
using Synentra.Domain.Agents;

namespace Synentra.Application.Abstractions.Security;

public interface IAgentAuthenticator
{
    AgentAuthResult Authenticate(Agent agent);

    Task<ClaimsPrincipal?> ValidateAsync(string credential, CancellationToken cancellationToken = default);
}