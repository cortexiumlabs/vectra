using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Policies.PoliciesList;

public class PoliciesListRequest : IRequest<PaginatedResult<PoliciesListResult>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}