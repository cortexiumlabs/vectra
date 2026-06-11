namespace Synentra.BuildingBlocks.Configuration.HumanInTheLoop;

/// <summary>
/// Configuration for Microsoft Teams notifications.
/// </summary>
public class TeamsNotificationConfiguration
{
    /// <summary>Whether Teams notifications are enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Teams webhook URL (incoming webhook connector).</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Optional theme color for the card (hex format).</summary>
    public string? ThemeColor { get; set; } = "0076D7";
}