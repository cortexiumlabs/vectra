using Synentra.Application.Abstractions.Executions;

namespace Synentra.Application.Features.Hitl.GetStatus;

public record GetStatusResult(string Id, string Status, PendingHitlRequest? Request);
