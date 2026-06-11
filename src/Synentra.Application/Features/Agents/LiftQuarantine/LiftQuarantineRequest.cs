using Vectra.Application.Abstractions.Dispatchers;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Agents.LiftQuarantine;

public class LiftQuarantineRequest : IRequest<Result<Abstractions.Dispatchers.Void>>
{
    public required string AgentId { get; set; }
}
