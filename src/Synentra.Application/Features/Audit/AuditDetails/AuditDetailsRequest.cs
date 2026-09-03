using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Audit.AuditDetails;

public class AuditDetailsRequest : IRequest<Result<AuditDetailsResult>>
{
    public long Id { get; set; }
}
