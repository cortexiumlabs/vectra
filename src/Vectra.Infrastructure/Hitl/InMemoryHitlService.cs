using System.Collections.Concurrent;
using Vectra.Application.Abstractions.Executions;
using Vectra.Application.Models;

namespace Vectra.Infrastructure.Hitl;

public class InMemoryHitlService : IHitlService
{
    private readonly ConcurrentDictionary<string, PendingHitlRequest> _pending = new();
    private readonly ConcurrentDictionary<string, string> _decisions = new();

    public Task<string> SuspendRequestAsync(RequestContext context, string reason, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString();
        var pending = new PendingHitlRequest(
            Id: id,
            Method: context.Method,
            Url: context.Path,
            Headers: context.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body: context.Body,
            AgentId: context.AgentId,
            Timestamp: DateTime.UtcNow
        );

        _pending[id] = pending;
        return Task.FromResult(id);
    }

    public Task<PendingHitlRequest?> GetPendingAsync(string id, CancellationToken cancellationToken = default)
    {
        _pending.TryGetValue(id, out var request);
        return Task.FromResult(request);
    }

    public Task ApproveAsync(string id, CancellationToken cancellationToken = default)
    {
        _decisions[id] = "approved";
        return Task.CompletedTask;
    }

    public Task DenyAsync(string id, CancellationToken cancellationToken = default)
    {
        _decisions[id] = "denied";
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        _pending.TryRemove(id, out _);
        _decisions.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}