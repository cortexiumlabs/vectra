using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Executions;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Hitl.GetAllPending;

public class GetAllPendingRequest : IRequest<PaginatedResult<PendingHitlRequest>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}