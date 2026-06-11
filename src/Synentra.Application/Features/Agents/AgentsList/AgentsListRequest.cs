using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Agents.AgentsList;

public class AgentsListRequest : IRequest<PaginatedResult<AgentsListResult>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}