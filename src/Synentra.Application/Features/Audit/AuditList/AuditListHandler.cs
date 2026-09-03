using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Persistence;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Audit.AuditList;

internal class AuditListHandler : IActionHandler<AuditListRequest, PaginatedResult<AuditListResult>>
{
    private readonly IAuditRepository _auditRepository;

    public AuditListHandler(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
    }

    public async Task<PaginatedResult<AuditListResult>> Handle(AuditListRequest request, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _auditRepository.GetPagedAsync(request.Page, request.PageSize, cancellationToken);

        var results = items.Select(a => new AuditListResult
        {
            Id = a.Id,
            AgentId = a.AgentId,
            Action = a.Action,
            TargetUrl = a.TargetUrl,
            Status = a.Status,
            RiskScore = a.RiskScore,
            Intent = a.Intent,
            Reason = a.Reason,
            Timestamp = a.Timestamp
        }).ToList();

        return await PaginatedResult<AuditListResult>.SuccessAsync(results, request.Page, request.PageSize, totalCount);
    }
}
