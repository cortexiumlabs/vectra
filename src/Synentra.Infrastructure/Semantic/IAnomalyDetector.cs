using Synentra.Application.Models;

namespace Synentra.Infrastructure.Semantic;

public interface IAnomalyDetector
{
    Task<double> DetectAsync(RequestContext context, CancellationToken cancellationToken);
}