using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Policies.PolicyDetails;

public class PolicyDetailsRequest : IRequest<Result<PolicyDetailsResult>>
{
    public required string Name { get; set; }
}
