using Vectra.Application.Abstractions.Dispatchers;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Agents.RegisterAgent;

public class CreateAgentRequest : IRequest<Result<CreateAgentResult>>
{
    public required string Name { get; set; }
    public required string OwnerId { get; set; }
    public required string ClientSecret { get; set; }
}