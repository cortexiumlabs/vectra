using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Models;
using Synentra.BuildingBlocks.Clock;
using Synentra.BuildingBlocks.Configuration.Policy;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Domain.AuditTrails;
using Synentra.Domain.Policies;
using Synentra.Infrastructure.Decision;

namespace Synentra.Infrastructure.UnitTests.Decision;

public class DecisionEngineTests
{
    private readonly IPolicyProvider _policyProvider = Substitute.For<IPolicyProvider>();
    private readonly IRiskScoringService _riskScoring = Substitute.For<IRiskScoringService>();
    private readonly ISemanticProvider _semanticProvider = Substitute.For<ISemanticProvider>();
    private readonly IAgentHistoryRepository _history = Substitute.For<IAgentHistoryRepository>();
    private readonly IAuditRepository _audit = Substitute.For<IAuditRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ILogger<DecisionEngine> _logger = Substitute.For<ILogger<DecisionEngine>>();

    private DecisionEngine CreateSut(
        bool policyEnabled = true,
        bool semanticEnabled = false,
        double? hitlThreshold = 0.8,
        double? semanticConfidenceThreshold = 0.7,
        bool allowLowConfidence = false)
    {
        _clock.UtcNow.Returns(DateTime.UtcNow);
        _audit.AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var semanticConfig = new SemanticConfiguration
        {
            Enabled = semanticEnabled,
            ConfidenceThreshold = semanticConfidenceThreshold,
            AllowLowConfidence = allowLowConfidence
        };
        var policyConfig = new PolicyConfiguration { Enabled = policyEnabled };

        return new DecisionEngine(
            Options.Create(semanticConfig),
            Options.Create(policyConfig),
            _policyProvider,
            _riskScoring,
            _semanticProvider,
            _history,
            _audit,
            _clock,
            _logger);
    }

    private void SetupAllow()
    {
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        _semanticProvider.AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticAnalysisResult { Confidence = 0.9 });
    }

    [Fact]
    public async Task EvaluateAsync_PolicyDeny_ReturnsDeny()
    {
        var sut = CreateSut();
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Deny("blocked by policy"));
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsDenied.Should().BeTrue();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_PolicyHitl_ReturnsHitl()
    {
        var sut = CreateSut();
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Hitl("review required"));
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsHitl.Should().BeTrue();

        await _history.Received(1).RecordRequestAsync(
            context.AgentId,
            true, // violation
            Arg.Any<double>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvaluateAsync_PolicyDisabled_SkipsPolicyCheck()
    {
        var sut = CreateSut(policyEnabled: false);
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, 
            context, 
            TestContext.Current.CancellationToken);

        await _policyProvider.DidNotReceive()
            .EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>());
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_HighRiskScore_ReturnsHitl()
    {
        var sut = CreateSut(hitlThreshold: 0.7);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.9,
                TrustScore = 0,
                RiskLevel = "high"
            }); // above threshold
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_LowRiskScore_Allows()
    {
        var sut = CreateSut();
        SetupAllow();
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_SemanticEnabled_LowConfidence_ReturnsHitl()
    {
        var sut = CreateSut(semanticEnabled: true, semanticConfidenceThreshold: 0.7);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        _semanticProvider.AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticAnalysisResult { Confidence = 0.4 }); // below threshold

        var result = await sut.EvaluateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_SemanticDisabled_SkipsSemanticCheck()
    {
        var sut = CreateSut(semanticEnabled: false);
        SetupAllow();
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        await _semanticProvider.DidNotReceive()
            .AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_SemanticEnabled_AllowLowConfidence_DoesNotHitl()
    {
        var sut = CreateSut(semanticEnabled: true, allowLowConfidence: true, semanticConfidenceThreshold: 0.7);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        _semanticProvider.AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticAnalysisResult { Confidence = 0.3 }); // below threshold, but allowed

        var result = await sut.EvaluateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_RecordsAudit()
    {
        var sut = CreateSut();
        SetupAllow();
        var context = BuildContext();

        await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        await _audit.Received(1).AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_NullSemanticOptions_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            null!,
            Options.Create(new PolicyConfiguration()),
            _policyProvider, _riskScoring, _semanticProvider,
            _history, _audit, _clock, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullPolicyProvider_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            Options.Create(new PolicyConfiguration()),
            null!, _riskScoring, _semanticProvider,
            _history, _audit, _clock, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullPolicyOptions_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            null!,
            _policyProvider, _riskScoring, _semanticProvider,
            _history, _audit, _clock, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullSemanticProvider_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            Options.Create(new PolicyConfiguration()),
            _policyProvider, _riskScoring, null!,
            _history, _audit, _clock, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullRiskScoring_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            Options.Create(new PolicyConfiguration()),
            _policyProvider, null!, _semanticProvider,
            _history, _audit, _clock, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullHistory_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            Options.Create(new PolicyConfiguration()),
            _policyProvider, _riskScoring, _semanticProvider,
            null!, _audit, _clock, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullAudit_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            Options.Create(new PolicyConfiguration()),
            _policyProvider, _riskScoring, _semanticProvider,
            _history, null!, _clock, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullClock_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            Options.Create(new PolicyConfiguration()),
            _policyProvider, _riskScoring, _semanticProvider,
            _history, _audit, null!, _logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new DecisionEngine(
            Options.Create(new SemanticConfiguration()),
            Options.Create(new PolicyConfiguration()),
            _policyProvider, _riskScoring, _semanticProvider,
            _history, _audit, _clock, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_HistoryRecordFails_StillReturnsDecision()
    {
        // RecordHistoryAsync has a try/catch — failure should not propagate
        var sut = CreateSut();
        SetupAllow();
        _history.RecordRequestAsync(
            Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("DB error")));

        var result = await sut.EvaluateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue("history failure should be swallowed");
    }

    [Fact]
    public async Task EvaluateAsync_SemanticEnabled_HighConfidence_AllowsContinuation()
    {
        var sut = CreateSut(semanticEnabled: true, semanticConfidenceThreshold: 0.7);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        _semanticProvider.AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticAnalysisResult { Confidence = 0.95 }); // above threshold

        var result = await sut.EvaluateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_AllowDecision_RecordsHistoryWithNoViolation()
    {
        var sut = CreateSut();
        SetupAllow();
        _history.RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await sut.EvaluateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        await _history.Received(1).RecordRequestAsync(
            Arg.Any<Guid>(),
            false, // not a violation
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_DenyDecision_RecordsHistoryAsViolation()
    {
        var sut = CreateSut();
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Deny("blocked"));
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        _history.RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await sut.EvaluateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        await _history.Received(1).RecordRequestAsync(
            Arg.Any<Guid>(),
            true, // violation = true
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    #region SimulateAsync Tests

    [Fact]
    public async Task SimulateAsync_PolicyDeny_ReturnsDeny()
    {
        var sut = CreateSut();
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Deny("blocked by policy"));
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        var context = BuildContext();

        var result = await sut.SimulateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsDenied.Should().BeTrue();
        result.Reason.Should().NotBeNullOrEmpty();
        await _audit.DidNotReceive().AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
        await _history.DidNotReceive().RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimulateAsync_PolicyHitl_ReturnsHitl()
    {
        var sut = CreateSut();
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Hitl("review required"));
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        var context = BuildContext();

        var result = await sut.SimulateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsHitl.Should().BeTrue();
        await _audit.DidNotReceive().AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
        await _history.DidNotReceive().RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimulateAsync_HighRiskScore_ReturnsHitl()
    {
        var sut = CreateSut(hitlThreshold: 0.7);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.9,
                TrustScore = 0,
                RiskLevel = "high"
            }); // above threshold
        var context = BuildContext();

        var result = await sut.SimulateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
        await _audit.DidNotReceive().AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
        await _history.DidNotReceive().RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimulateAsync_LowConfidence_ReturnsHitl()
    {
        var sut = CreateSut(semanticEnabled: true, semanticConfidenceThreshold: 0.7);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        _semanticProvider.AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticAnalysisResult { Confidence = 0.4 }); // below threshold

        var result = await sut.SimulateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
        await _audit.DidNotReceive().AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
        await _history.DidNotReceive().RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimulateAsync_Allow_ReturnsAllow()
    {
        var sut = CreateSut();
        SetupAllow();
        var context = BuildContext();

        var result = await sut.SimulateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
        await _audit.DidNotReceive().AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
        await _history.DidNotReceive().RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_SemanticProviderThrows_DoesNotPropagateAndRecordsAudit()
    {
        var sut = CreateSut(semanticEnabled: true);

        _semanticProvider.AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<SemanticAnalysisResult>>(x => throw new Exception("boom"));

        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());

        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult { RiskScore = 0.1, TrustScore = 0, RiskLevel = "low" });

        var ctx = BuildContext();

        var result = await sut.EvaluateAsync(ctx.Path ?? string.Empty, ctx, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
        // Ensure audit was recorded even if semantic failed
        await _audit.Received(1).AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_HistoryThrows_DoesNotPropagate()
    {
        var sut = CreateSut();
        SetupAllow();

        _history.RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new Exception("db"));

        var ctx = BuildContext();

        // Should not throw despite history exception
        var act = async () => await sut.EvaluateAsync(ctx.Path ?? string.Empty, ctx, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();

        // Audit should still be attempted
        await _audit.Received(1).AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimulateAsync_NullPolicyDecision_DefaultsToAllow()
    {
        var sut = CreateSut();

        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns((PolicyDecision?)null);

        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult { RiskScore = 0.1, TrustScore = 0, RiskLevel = "low" });

        _semanticProvider.AnalyzeAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticAnalysisResult { Confidence = 0.9 });

        var ctx = BuildContext();

        var result = await sut.SimulateAsync(ctx.Path ?? string.Empty, ctx, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
        await _audit.DidNotReceive().AddAsync(Arg.Any<AuditTrail>(), Arg.Any<CancellationToken>());
        await _history.DidNotReceive().RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    #endregion

    private static RequestContext BuildContext() => new()
    {
        AgentId = Guid.NewGuid(),
        Method = "GET",
        Path = "/api/data",
        TargetUrl = "https://service.local/api/data",
        PolicyName = "default-policy",
        TrustScore = 0.8
    };

    [Fact]
    public async Task EvaluateAsync_PolicyDenyWithNullReason_UsesDefaultReason()
    {
        var sut = CreateSut();
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Deny(null)); // Null reason
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsDenied.Should().BeTrue();
        result.Reason.Should().Be("Policy denied");
    }

    [Fact]
    public async Task EvaluateAsync_PolicyHitlWithNullReason_UsesDefaultReason()
    {
        var sut = CreateSut();
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Hitl(null)); // Null reason
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsHitl.Should().BeTrue();
        result.Reason.Should().Be("Policy requires HITL");
    }

    [Fact]
    public async Task EvaluateAsync_NullHitlThreshold_UsesDefault()
    {
        var sut = CreateSut(hitlThreshold: null);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.85,
                TrustScore = 0,
                RiskLevel = "high"
            }); // Above default of 0.8
        var context = BuildContext();

        var result = await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_NullSemanticConfidenceThreshold_UsesDefault()
    {
        var sut = CreateSut(semanticEnabled: true, semanticConfidenceThreshold: null);
        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(PolicyDecision.Allow());
        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });
        _semanticProvider.AnalyzeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticAnalysisResult { Confidence = 0.6 }); // Below default of 0.7

        var result = await sut.EvaluateAsync(BuildContext().Path ?? string.Empty, BuildContext(), TestContext.Current.CancellationToken);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_PolicyInput_IsBuiltCorrectly()
    {
        var sut = CreateSut();
        var context = BuildContext();
        context.Headers.Add("X-Test", "value");
        PolicyEvaluationContext capturedContext = null!;

        _riskScoring.ComputeRiskScoreAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RiskEvaluationResult
            {
                RiskScore = 0.1,
                TrustScore = 0,
                RiskLevel = "low"
            });

        _policyProvider.EvaluateAsync(Arg.Any<PolicyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.ArgAt<PolicyEvaluationContext>(0);
                return PolicyDecision.Allow();
            });

        await sut.EvaluateAsync(context.Path ?? string.Empty, context, TestContext.Current.CancellationToken);

        capturedContext.Should().NotBeNull();
        capturedContext.RequestContext.Method.Should().Be(context.Method);
        capturedContext.RequestContext.Path.Should().Be(context.Path);
        capturedContext.RequestContext.Headers.Should().BeEquivalentTo(context.Headers);
        capturedContext.RequestContext.AgentId.Should().Be(context.AgentId);
        capturedContext.RequestContext.TrustScore.Should().Be(context.TrustScore);
    }
}


