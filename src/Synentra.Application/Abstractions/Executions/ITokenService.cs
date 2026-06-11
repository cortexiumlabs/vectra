using System.Security.Claims;
using Vectra.Domain.Agents;

namespace Vectra.Application.Abstractions.Executions;

public interface ITokenService
{
    string GenerateToken(Agent agent);
    ClaimsPrincipal? ValidateToken(string token);
}