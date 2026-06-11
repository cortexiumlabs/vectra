using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Agents.DeleteAgent;

public class DeleteAgentRequest : IRequest<Result<Abstractions.Dispatchers.Void>>
{
    public required string AgentId { get; set; }
}