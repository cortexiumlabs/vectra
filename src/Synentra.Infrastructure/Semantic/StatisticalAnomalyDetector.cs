using Vectra.Application.Abstractions.Persistence;
using Vectra.Application.Models;

namespace Vectra.Infrastructure.Semantic;

public class StatisticalAnomalyDetector : IAnomalyDetector
{
    private readonly IAgentHistoryRepository _agentHistoryRepository;

    public StatisticalAnomalyDetector(IAgentHistoryRepository agentHistoryRepository)
    {
        _agentHistoryRepository = agentHistoryRepository ?? throw new ArgumentNullException(nameof(agentHistoryRepository));
    }

    public async Task<double> DetectAsync(RequestContext context, CancellationToken cancellationToken)
    {
        var baseline = await _agentHistoryRepository.GetBaselineAsync(context.AgentId, TimeSpan.FromDays(7), cancellationToken);
        if (baseline == null) return 0.0;

        var current = await _agentHistoryRepository.GetStatsAsync(context.AgentId, TimeSpan.FromMinutes(5), cancellationToken);
        if (current == null) return 0.0;

        double anomaly = 0.0;
        // Compare current rate to baseline
        if (baseline.AverageRequestsPerMinute > 0)
        {
            var rateRatio = current.CurrentRequestsPerMinute / baseline.AverageRequestsPerMinute;
            if (rateRatio > 3.0) anomaly += 0.6;
            else if (rateRatio > 2.0) anomaly += 0.3;
        }
        // ... other comparisons
        return Math.Min(1.0, anomaly);
    }
}