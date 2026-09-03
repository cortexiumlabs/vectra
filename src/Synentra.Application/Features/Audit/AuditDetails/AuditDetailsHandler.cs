using Synentra.Application.Abstractions.Dispatchers;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Errors;
using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Audit.AuditDetails;

internal class AuditDetailsHandler : IActionHandler<AuditDetailsRequest, Result<AuditDetailsResult>>
{
    private readonly IAuditRepository _auditRepository;

    public AuditDetailsHandler(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
    }

    public async Task<Result<AuditDetailsResult>> Handle(AuditDetailsRequest request, CancellationToken cancellationToken = default)
    {
        var audit = await _auditRepository.GetByIdAsync(request.Id, cancellationToken);

        if (audit is null)
        {
            return await Result<AuditDetailsResult>.FailureAsync(
                Error.NotFound(ApplicationErrorCodes.AuditTrailNotFound, $"Audit trail '{request.Id}' was not found."));
        }

        var result = new AuditDetailsResult
        {
            Id = audit.Id,
            AgentId = audit.AgentId,
            Action = audit.Action,
            TargetUrl = audit.TargetUrl,
            Status = audit.Status,
            RiskScore = audit.RiskScore,
            Intent = audit.Intent,
            Reason = audit.Reason,
            Timestamp = audit.Timestamp
        };

        return await Result<AuditDetailsResult>.SuccessAsync(result);
    }
}
