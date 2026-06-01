using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;

namespace Vectra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Sends HITL notifications to a generic HTTP webhook endpoint.
/// This provides backward compatibility with the old NotificationWebhookUrl configuration.
/// </summary>
public class GenericWebhookNotifier : IHitlNotifier
{
    private readonly GenericWebhookNotificationConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GenericWebhookNotifier> _logger;

    public GenericWebhookNotifier(
        IOptions<HumanInTheLoopConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<GenericWebhookNotifier> logger)
    {
        var hitlConfig = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _config = hitlConfig.Notifications?.GenericWebhook ?? new GenericWebhookNotificationConfiguration();
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(HitlNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.WebhookUrl))
        {
            _logger.LogDebug("Generic webhook notifications are disabled or webhook URL is not configured");
            return;
        }

        var webhookUrl = _config.WebhookUrl;

        try
        {
            var payload = new
            {
                notification.Id,
                AgentId = notification.AgentId.ToString(),
                notification.Method,
                notification.Url,
                notification.Reason,
                notification.Timestamp,
                notification.ExpiresAt
            };

            var httpClient = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = JsonContent.Create(payload)
            };

            // Add custom headers if configured
            if (_config.Headers is not null)
            {
                foreach (var (key, value) in _config.Headers)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Generic webhook notification for HITL request {HitlId} failed with status {StatusCode}: {Error}",
                    notification.Id, (int)response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("Generic webhook notification sent successfully for HITL request {HitlId}", notification.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send generic webhook notification for HITL request {HitlId}", notification.Id);
        }
    }
}
