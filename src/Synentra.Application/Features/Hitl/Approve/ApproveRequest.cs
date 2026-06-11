using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Hitl.Approve;

public class ApproveRequest : IRequest<Result<ApproveResult>>
{
    public required string Id { get; set; }
    public required string ReviewerId { get; set; }
    public string? Comment { get; set; }
}
