using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Agents.RegisterAgent;

public class CreateAgentRequest : IRequest<Result<CreateAgentResult>>
{
    public required string Name { get; set; }
    public required string OwnerId { get; set; }
    public required string ClientSecret { get; set; }
}