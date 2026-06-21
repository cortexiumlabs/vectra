using System.Reflection;
using Synentra.Application.Abstractions.Versioning;

namespace Synentra.Services;

public class SynentraVersion : IVersion
{
    private readonly ILogger<SynentraVersion> _logger;

    public SynentraVersion(ILogger<SynentraVersion> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Version Version => GetApplicationVersion(_logger);

    public static Version GetApplicationVersion(ILogger? logger = null)
    {
        try
        {
            var attributes = Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);

            var versionString = attributes.Length == 0
                ? "0.0.0.0" // Default if no version is found
                : ((AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion;

            // Parse the string to a Version object
            if (Version.TryParse(versionString, out var version))
            {
                return version;
            }

            // Fallback if parsing fails
            logger?.LogWarning("Failed to parse application version. Using default 0.0.0.0.");
            return new Version(0, 0, 0, 0);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse application version. Using default 0.0.0.0.");
            return new Version(0, 0, 0, 0);
        }
    }
}