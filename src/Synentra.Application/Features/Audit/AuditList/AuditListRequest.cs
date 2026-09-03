using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Audit.AuditList;

public class AuditListRequest : IRequest<PaginatedResult<AuditListResult>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
