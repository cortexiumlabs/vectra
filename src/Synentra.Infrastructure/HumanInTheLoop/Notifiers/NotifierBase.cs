using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Vectra.Application.Abstractions.Executions;

namespace Vectra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Base class for HITL notifiers that provides common functionality for sending notifications.
/// </summary>
/// <typeparam name="TPayload">The type of payload to send.</typeparam>
public abstract class NotifierBase<TPayload> : IHitlNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    protected NotifierBase(
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(HitlNotification notification, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            _logger.LogDebug("{NotifierType} notifications are disabled or not configured", GetNotifierType());
            return;
        }

        try
        {
            var payload = BuildPayload(notification);
            var webhookUrl = GetWebhookUrl();
            var httpClient = _httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = JsonContent.Create(payload)
            };

            // Allow derived classes to add custom headers
            ConfigureRequest(request, notification);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "{NotifierType} notification for HITL request {HitlId} failed with status {StatusCode}: {Error}",
                    GetNotifierType(), notification.Id, (int)response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("{NotifierType} notification sent successfully for HITL request {HitlId}",
                    GetNotifierType(), notification.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {NotifierType} notification for HITL request {HitlId}",
                GetNotifierType(), notification.Id);
        }
    }

    /// <summary>
    /// Determines if the notifier is enabled and properly configured.
    /// </summary>
    protected abstract bool IsEnabled();

    /// <summary>
    /// Gets the webhook URL to send the notification to.
    /// </summary>
    protected abstract string GetWebhookUrl();

    /// <summary>
    /// Builds the notification payload specific to this notifier.
    /// </summary>
    protected abstract TPayload BuildPayload(HitlNotification notification);

    /// <summary>
    /// Gets the notifier type name for logging purposes.
    /// </summary>
    protected abstract string GetNotifierType();

    /// <summary>
    /// Configures the HTTP request before sending. Override to add custom headers or other modifications.
    /// </summary>
    protected virtual void ConfigureRequest(HttpRequestMessage request, HitlNotification notification)
    {
        // Default implementation does nothing - derived classes can override to add custom headers
    }
}
