using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Authentications.GenerateToken;

public class GenerateTokenRequest : IRequest<Result<GenerateTokenResult>>
{
    public Guid AgentId { get; set; }
    public string ClientSecret { get; set; } = string.Empty;
}
