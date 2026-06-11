using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Hitl.GetStatus;

public class GetStatusRequest : IRequest<Result<GetStatusResult>>
{
    public required string Id { get; set; }
}
