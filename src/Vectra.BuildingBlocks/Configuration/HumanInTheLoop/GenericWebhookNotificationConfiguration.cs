namespace Vectra.BuildingBlocks.Configuration.HumanInTheLoop;

/// <summary>
/// Configuration for generic webhook notifications.
/// </summary>
public class GenericWebhookNotificationConfiguration
{
    /// <summary>Whether generic webhook notifications are enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Webhook URL to POST to.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Optional custom headers to include in the request.</summary>
    public Dictionary<string, string>? Headers { get; set; }
}
