using Vectra.Application.Abstractions.Dispatchers;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Policies.PoliciesList;

public class PoliciesListRequest : IRequest<PaginatedResult<PoliciesListResult>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}