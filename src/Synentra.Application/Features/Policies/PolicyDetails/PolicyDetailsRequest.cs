using Vectra.Application.Abstractions.Dispatchers;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Policies.PolicyDetails;

public class PolicyDetailsRequest : IRequest<Result<PolicyDetailsResult>>
{
    public required string Name { get; set; }
}
