using System.Security.Claims;
using Synentra.Domain.Agents;

namespace Synentra.Application.Abstractions.Executions;

public interface ITokenService
{
    string GenerateToken(Agent agent);
    ClaimsPrincipal? ValidateToken(string token);
}