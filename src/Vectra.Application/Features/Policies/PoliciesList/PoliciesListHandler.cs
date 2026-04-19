using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Policies.PoliciesList;

internal class PoliciesListHandler : IActionHandler<PoliciesListRequest, PaginatedResult<PoliciesListResult>>
{
    private readonly IPolicyCacheService _policyCacheService;

    public PoliciesListHandler(IPolicyCacheService policyCacheService)
    {
        _policyCacheService = policyCacheService ?? throw new ArgumentNullException(nameof(policyCacheService));
    }

    public async Task<PaginatedResult<PoliciesListResult>> Handle(PoliciesListRequest request, CancellationToken cancellationToken)
    {
        var (policies, totalCount) = await _policyCacheService.GetPagedAsync(request.Page, request.PageSize, cancellationToken);

        var items = policies.Select(p => new PoliciesListResult
        {
            PolicyName = p.Name,
            Description = p.Description ?? string.Empty,
            Owner = p.Owner
        }).ToList();

        return PaginatedResult<PoliciesListResult>.Success(items, request.Page, request.PageSize, totalCount);
    }
}