using System.Security.Claims;
using Vectra.Domain.Agents;

namespace Vectra.Application.Abstractions.Security;

public interface IAgentAuthenticator
{
    AgentAuthResult Authenticate(Agent agent);

    Task<ClaimsPrincipal?> ValidateAsync(string credential, CancellationToken cancellationToken = default);
}