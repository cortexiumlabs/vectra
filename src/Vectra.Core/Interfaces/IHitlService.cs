using Vectra.Core.DTOs;

namespace Vectra.Core.Interfaces;

public interface IHitlService
{
    Task<string> SuspendRequestAsync(RequestContext context, string reason, CancellationToken cancellationToken = default);
    Task<PendingHitlRequest?> GetPendingAsync(string id, CancellationToken cancellationToken = default);
    Task ApproveAsync(string id, CancellationToken cancellationToken = default);
    Task DenyAsync(string id, CancellationToken cancellationToken = default);
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);
}

public record PendingHitlRequest(
    string Id,
    string Method,
    string Url,
    Dictionary<string, string> Headers,
    string? Body,
    Guid AgentId,
    DateTime Timestamp);