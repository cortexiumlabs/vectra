using Synentra.Application.Models;
using Synentra.Domain.Agents;

namespace Synentra.Infrastructure.Risk;

public interface IRiskCalculator
{
    string Name { get; }
    double Weight { get; }  // configurable weight
    Task<double> CalculateAsync(RequestContext context, AgentHistory? history, CancellationToken cancellationToken);
}