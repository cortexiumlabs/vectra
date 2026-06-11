using Vectra.Application.Models;

namespace Vectra.Application.Abstractions.Executions;

/// <summary>
/// Abstraction for sending real-time notifications when a HITL request is suspended for review.
/// Implementations can send to Slack, Microsoft Teams, PagerDuty, or generic webhooks.
/// </summary>
public interface IHitlNotifier
{
    /// <summary>
    /// Sends a notification for a newly suspended HITL request.
    /// </summary>
    /// <param name="notification">The notification details to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyAsync(HitlNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a HITL notification to be sent to reviewers.
/// </summary>
public record HitlNotification(
    string Id,
    Guid AgentId,
    string Method,
    string Url,
    string Reason,
    DateTime Timestamp,
    DateTime ExpiresAt);
