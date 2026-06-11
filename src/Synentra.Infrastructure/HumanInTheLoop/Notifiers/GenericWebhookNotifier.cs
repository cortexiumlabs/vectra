using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Executions;
using Synentra.BuildingBlocks.Configuration.HumanInTheLoop;

namespace Synentra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Sends HITL notifications to a generic HTTP webhook endpoint.
/// This provides backward compatibility with the old NotificationWebhookUrl configuration.
/// </summary>
public class GenericWebhookNotifier : NotifierBase<object>
{
    private readonly GenericWebhookNotificationConfiguration _config;

    public GenericWebhookNotifier(
        IOptions<HumanInTheLoopConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<GenericWebhookNotifier> logger)
        : base(httpClientFactory, logger)
    {
        var hitlConfig = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _config = hitlConfig.Notifications?.GenericWebhook ?? new GenericWebhookNotificationConfiguration();
    }

    protected override bool IsEnabled() => _config.Enabled && !string.IsNullOrWhiteSpace(_config.WebhookUrl);

    protected override string GetWebhookUrl() => _config.WebhookUrl!;

    protected override string GetNotifierType() => "Generic webhook";

    protected override object BuildPayload(HitlNotification notification)
    {
        return new
        {
            notification.Id,
            AgentId = notification.AgentId.ToString(),
            notification.Method,
            notification.Url,
            notification.Reason,
            notification.Timestamp,
            notification.ExpiresAt
        };
    }

    protected override void ConfigureRequest(HttpRequestMessage request, HitlNotification notification)
    {
        // Add custom headers if configured
        if (_config.Headers is not null)
        {
            foreach (var (key, value) in _config.Headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }
    }
}
