using System.Security.Claims;
using Vectra.Core.Entities;

namespace Vectra.Core.Interfaces;

public interface ITokenService
{
    string GenerateToken(Agent agent);
    ClaimsPrincipal? ValidateToken(string token);
}