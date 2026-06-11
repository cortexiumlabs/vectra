using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;

namespace Vectra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Sends HITL notifications to Slack via incoming webhooks.
/// </summary>
public class SlackNotifier : NotifierBase<SlackNotifier.SlackPayload>
{
    private readonly SlackNotificationConfiguration _config;

    public SlackNotifier(
        IOptions<HumanInTheLoopConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<SlackNotifier> logger)
        : base(httpClientFactory, logger)
    {
        _config = config?.Value?.Notifications?.Slack ?? throw new ArgumentNullException(nameof(config));
    }

    protected override bool IsEnabled() => _config.Enabled && !string.IsNullOrWhiteSpace(_config.WebhookUrl);

    protected override string GetWebhookUrl() => _config.WebhookUrl!;

    protected override string GetNotifierType() => "Slack";

    protected override SlackPayload BuildPayload(HitlNotification notification)
    {
        var expiresInMinutes = (int)(notification.ExpiresAt - notification.Timestamp).TotalMinutes;

        var text = $"🚨 *HITL Review Required*\n\n" +
                   $"*Request ID:* `{notification.Id}`\n" +
                   $"*Agent ID:* `{notification.AgentId}`\n" +
                   $"*Method:* `{notification.Method}`\n" +
                   $"*URL:* `{notification.Url}`\n" +
                   $"*Reason:* {notification.Reason}\n" +
                   $"*Expires In:* {expiresInMinutes} minutes\n" +
                   $"*Timestamp:* {notification.Timestamp:yyyy-MM-dd HH:mm:ss} UTC";

        return new SlackPayload
        {
            Text = text,
            Username = _config.Username,
            IconEmoji = _config.IconEmoji,
            Channel = _config.Channel
        };
    }

    public record SlackPayload
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("username")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Username { get; init; }

        [JsonPropertyName("icon_emoji")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? IconEmoji { get; init; }

        [JsonPropertyName("channel")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Channel { get; init; }
    }
}
