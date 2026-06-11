using Synentra.Application.Abstractions.Dispatchers;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.Features.Agents.AssignPolicy;

public class AssignPolicyRequest : AssignPolicyRequestModel, IRequest<Result<Abstractions.Dispatchers.Void>>
{
    public required string AgentId { get; set; }
}

public class AssignPolicyRequestModel
{
    public required string PolicyName { get; set; }
}