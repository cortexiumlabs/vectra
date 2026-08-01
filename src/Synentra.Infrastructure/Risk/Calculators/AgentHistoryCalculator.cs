using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Models;

namespace Synentra.Infrastructure.Risk.Calculators;

public class AgentHistoryCalculator : IRiskCalculator
{
    public string Name => "AgentHistoryRisk";
    public double Weight { get; set; } = 0.15;

    private readonly IAgentHistoryRepository _historyRepo;
    public AgentHistoryCalculator(IAgentHistoryRepository historyRepo)
    {
        _historyRepo = historyRepo;
    }

    public async Task<RiskCalculatorResult> CalculateAsync(RiskEvaluationContext context, CancellationToken cancellationToken)
    {
        var requestContext = context.RequestContext;

        var history = await _historyRepo.GetRecentAsync(requestContext.AgentId, TimeSpan.FromMinutes(5), cancellationToken);

        if (history == null) return RiskCalculatorResult.Create(Name, 0.3, Weight); // unknown

        double risk = 0.0;
        // Factor 1: violation rate in last 5 minutes
        if (history.TotalRequests > 0)
        {
            var violationRate = history.ViolationCount / (double)history.TotalRequests;
            risk += violationRate * 0.5;
        }
        // Factor 2: request frequency (too high = risky)
        var rpm = history.TotalRequests / 5.0; // requests per minute (over 5 min window)
        if (rpm > 60) risk += 0.3;
        else if (rpm > 30) risk += 0.15;
        else if (rpm > 10) risk += 0.05;

        // Factor 3: trust score decay (if agent trust score is low, increase risk)
        if (requestContext.TrustScore < 0.3) risk += 0.4;
        else if (requestContext.TrustScore < 0.6) risk += 0.2;

        return RiskCalculatorResult.Create(Name, Math.Min(1.0, risk), Weight);
    }
}