using Microsoft.Extensions.Logging;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Abstractions.Executions;
using Vectra.Application.Errors;
using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.Features.Policies.PolicyDetails;

internal class PolicyDetailsHandler : IActionHandler<PolicyDetailsRequest, Result<PolicyDetailsResult>>
{
    private readonly ILogger<PolicyDetailsHandler> _logger;
    private readonly IPolicyCacheService _policyCacheService;

    public PolicyDetailsHandler(
        ILogger<PolicyDetailsHandler> logger,
        IPolicyCacheService policyCacheService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _policyCacheService = policyCacheService ?? throw new ArgumentNullException(nameof(policyCacheService));
    }

    public async Task<Result<PolicyDetailsResult>> Handle(PolicyDetailsRequest request, CancellationToken cancellationToken)
    {
        var (policies, _) = await _policyCacheService.GetPagedAsync(1, int.MaxValue, cancellationToken);

        var policy = policies.FirstOrDefault(p => string.Equals(p.Name, request.Name, StringComparison.OrdinalIgnoreCase));

        if (policy is null)
        {
            _logger.LogWarning("Policy {PolicyName} was not found", request.Name);
            return await Result<PolicyDetailsResult>.FailureAsync(
                Error.NotFound(ApplicationErrorCodes.PolicyNotFound, $"Policy '{request.Name}' was not found."));
        }

        var result = new PolicyDetailsResult
        {
            Name = policy.Name,
            Description = policy.Description,
            Owner = policy.Owner,
            CreatedOn = policy.CreatedOn,
            Default = policy.Default,
            Rules = policy.Rules
        };

        return await Result<PolicyDetailsResult>.SuccessAsync(result);
    }
}
