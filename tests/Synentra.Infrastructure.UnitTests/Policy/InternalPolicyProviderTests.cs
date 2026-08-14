using FluentAssertions;
using NSubstitute;
using Synentra.Application.Abstractions.Caches;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Models;
using Synentra.Domain.Policies;
using Synentra.Infrastructure.Caches;
using Synentra.Infrastructure.Policy.Providers;

namespace Synentra.Infrastructure.UnitTests.Policy;

public class InternalPolicyProviderTests
{
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ICacheProvider _cacheProvider = Substitute.For<ICacheProvider>();
    private readonly IPolicyLoader _loader = Substitute.For<IPolicyLoader>();
    private readonly InternalPolicyProvider _sut;

    public InternalPolicyProviderTests()
    {
        _cacheService.Current.Returns(_cacheProvider);
        _cacheProvider.TryGetValueAsync<Dictionary<string, PolicyDefinition>>(Arg.Any<string>())
            .Returns((false, null));
        _loader.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PolicyDefinition>());

        _sut = new InternalPolicyProvider(_cacheService, _loader);
    }

    [Fact]
    public async Task EvaluateAsync_EmptyPolicyName_ReturnsAllow()
    {
        var result = await _sut.EvaluateAsync(BuildEvaluationContext(string.Empty), CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_NullPolicyName_ReturnsAllow()
    {
        var result = await _sut.EvaluateAsync(BuildEvaluationContext(null), CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_PolicyNotFound_ReturnsDeny()
    {
        var result = await _sut.EvaluateAsync(BuildEvaluationContext("non-existent-policy"), CancellationToken.None);

        result.IsDenied.Should().BeTrue();
        result.Reason.Should().Contain("non-existent-policy");
    }

    [Fact]
    public async Task EvaluateAsync_PolicyWithDenyRule_MatchingCondition_ReturnsDeny()
    {
        var policy = new PolicyDefinition
        {
            Name = "test-policy",
            Default = PolicyType.Allow,
            Rules =
            [
                new PolicyRule
                {
                    Priority = 10,
                    Effect = PolicyType.Deny,
                    Reason = "method not allowed",
                    Conditions = [new PolicyRuleCondition { Field = "input.method", Operator = "eq", Value = "DELETE" }]
                }
            ]
        };
        SetupLoader(policy);

        var input = new Dictionary<string, object> { ["method"] = "DELETE" };
        var result = await _sut.EvaluateAsync(BuildEvaluationContext("test-policy", input), CancellationToken.None);

        result.IsDenied.Should().BeTrue();
        result.Reason.Should().Be("method not allowed");
    }

    [Fact]
    public async Task EvaluateAsync_PolicyWithAllowRule_MatchingCondition_ReturnsAllow()
    {
        var policy = new PolicyDefinition
        {
            Name = "test-policy",
            Default = PolicyType.Deny,
            Rules =
            [
                new PolicyRule
                {
                    Priority = 10,
                    Effect = PolicyType.Allow,
                    Reason = "GET is fine",
                    Conditions = [new PolicyRuleCondition { Field = "input.method", Operator = "eq", Value = "GET" }]
                }
            ]
        };
        SetupLoader(policy);

        var input = new Dictionary<string, object> { ["method"] = "GET" };
        var result = await _sut.EvaluateAsync(BuildEvaluationContext("test-policy", input), CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
        result.Reason.Should().Be("GET is fine");
    }

    [Fact]
    public async Task EvaluateAsync_PolicyWithHitlRule_MatchingCondition_ReturnsHitl()
    {
        var policy = new PolicyDefinition
        {
            Name = "test-policy",
            Default = PolicyType.Allow,
            Rules =
            [
                new PolicyRule
                {
                    Priority = 10,
                    Effect = PolicyType.Hitl,
                    Reason = "needs review",
                    Conditions = [new PolicyRuleCondition { Field = "input.method", Operator = "eq", Value = "POST" }]
                }
            ]
        };
        SetupLoader(policy);

        var input = new Dictionary<string, object> { ["method"] = "POST" };
        var result = await _sut.EvaluateAsync(BuildEvaluationContext("test-policy", input), CancellationToken.None);

        result.IsHitl.Should().BeTrue();
        result.Reason.Should().Be("needs review");
    }

    [Fact]
    public async Task EvaluateAsync_RuleConditionNotMatched_FallsToDefaultAllow()
    {
        var policy = new PolicyDefinition
        {
            Name = "test-policy",
            Default = PolicyType.Allow,
            Rules =
            [
                new PolicyRule
                {
                    Priority = 10,
                    Effect = PolicyType.Deny,
                    Conditions = [new PolicyRuleCondition { Field = "input.method", Operator = "eq", Value = "DELETE" }]
                }
            ]
        };
        SetupLoader(policy);

        var input = new Dictionary<string, object> { ["method"] = "GET" };
        var result = await _sut.EvaluateAsync(BuildEvaluationContext("test-policy", input), CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_RuleConditionNotMatched_FallsToDefaultDeny()
    {
        var policy = new PolicyDefinition
        {
            Name = "test-policy",
            Default = PolicyType.Deny,
            Rules =
            [
                new PolicyRule
                {
                    Priority = 10,
                    Effect = PolicyType.Allow,
                    Conditions = [new PolicyRuleCondition { Field = "input.method", Operator = "eq", Value = "GET" }]
                }
            ]
        };
        SetupLoader(policy);

        var input = new Dictionary<string, object> { ["method"] = "DELETE" };
        var result = await _sut.EvaluateAsync(BuildEvaluationContext("test-policy", input), CancellationToken.None);

        result.IsDenied.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_HigherPriorityRuleEvaluatedFirst()
    {
        var policy = new PolicyDefinition
        {
            Name = "test-policy",
            Default = PolicyType.Allow,
            Rules =
            [
                new PolicyRule
                {
                    Priority = 5,
                    Effect = PolicyType.Allow,
                    Reason = "low priority allow",
                    Conditions = [new PolicyRuleCondition { Field = "input.method", Operator = "eq", Value = "DELETE" }]
                },
                new PolicyRule
                {
                    Priority = 10,
                    Effect = PolicyType.Deny,
                    Reason = "high priority deny",
                    Conditions = [new PolicyRuleCondition { Field = "input.method", Operator = "eq", Value = "DELETE" }]
                }
            ]
        };
        SetupLoader(policy);

        var input = new Dictionary<string, object> { ["method"] = "DELETE" };
        var result = await _sut.EvaluateAsync(BuildEvaluationContext("test-policy", input), CancellationToken.None);

        result.IsDenied.Should().BeTrue();
        result.Reason.Should().Be("high priority deny");
    }

    [Fact]
    public async Task EvaluateAsync_CacheHit_DoesNotCallLoader()
    {
        var policies = new Dictionary<string, PolicyDefinition>
        {
            ["my-policy"] = new PolicyDefinition { Name = "my-policy", Default = PolicyType.Allow }
        };
        _cacheProvider.TryGetValueAsync<Dictionary<string, PolicyDefinition>>(Arg.Any<string>())
            .Returns((true, policies));

        await _sut.EvaluateAsync(BuildEvaluationContext("my-policy"), CancellationToken.None);

        await _loader.DidNotReceive().LoadAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_CacheMiss_CallsLoaderAndCaches()
    {
        var policy = new PolicyDefinition { Name = "my-policy", Default = PolicyType.Allow };
        _cacheProvider.TryGetValueAsync<Dictionary<string, PolicyDefinition>>(Arg.Any<string>())
            .Returns((false, null));
        _loader.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PolicyDefinition> { ["my-policy"] = policy });

        await _sut.EvaluateAsync(BuildEvaluationContext("my-policy"), CancellationToken.None);

        await _loader.Received(1).LoadAllAsync(Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).SetAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, PolicyDefinition>>());
    }

    [Fact]
    public async Task EvaluateAsync_CacheMiss_PropagatesCancellationTokenToLoader()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        await _sut.EvaluateAsync(
            BuildEvaluationContext("non-existent-policy"),
            cancellationToken);

        await _loader.Received(1).LoadAllAsync(cancellationToken);
    }

    [Fact]
    public async Task EvaluateAsync_DefaultHitl_ReturnsHitlWhenNoRuleMatches()
    {
        var policy = new PolicyDefinition
        {
            Name = "test-policy",
            Default = PolicyType.Hitl,
            Rules = []
        };
        SetupLoader(policy);

        var result = await _sut.EvaluateAsync(BuildEvaluationContext("test-policy"), CancellationToken.None);

        result.IsHitl.Should().BeTrue();
    }

    private void SetupLoader(PolicyDefinition policy)
    {
        _loader.LoadAllAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PolicyDefinition> { [policy.Name] = policy });
    }

    private static PolicyEvaluationContext BuildEvaluationContext(
        string? policyName,
        Dictionary<string, object>? input = null)
    {
        var method = input is not null && input.TryGetValue("method", out var methodValue)
            ? methodValue?.ToString() ?? string.Empty
            : "GET";

        var path = input is not null && input.TryGetValue("path", out var pathValue)
            ? pathValue?.ToString() ?? "/"
            : "/";

        return new PolicyEvaluationContext
        {
            RequestContext = new RequestContext
            {
                AgentId = Guid.NewGuid(),
                Method = method,
                Path = path,
                PolicyName = policyName ?? string.Empty,
                Headers = new Dictionary<string, string>()
            },
            Intent = new IntentClassificationResult
            {
                Label = "suspicious",
                Confidence = 0,
                Status = IntentClassificationStatus.Unavailable
            },
            Risk = new RiskEvaluationResult
            {
                RiskScore = 0,
                TrustScore = 0,
                RiskLevel = "low"
            }
        };
    }
}
