namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

public static class ModelPathResolver
{
    private const string DefaultModelFileName = "community-model.zip";
    // Exposed for unit tests so we can simulate different folder resolution behaviors.
    internal static Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string?> FolderGetter =
        Environment.GetFolderPath;

    /// <summary>
    /// Returns the absolute path to the model package, providing a default if the input is empty.
    /// </summary>
    public static string GetFullPackagePath(string? packagePath)
    {
        // 1. Use default if the path is missing or blank
        if (string.IsNullOrWhiteSpace(packagePath))
            packagePath = GetDefaultPackagePath();

        // 2. Expand environment variables (e.g., %LOCALAPPDATA%)
        string expanded = Environment.ExpandEnvironmentVariables(packagePath);

        // 3. If still relative, make it absolute using the application's base directory
        if (!Path.IsPathRooted(expanded))
            expanded = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));

        return expanded;
    }

    private static string GetDefaultPackagePath()
    {
        // 1. Try the per‑user local application data folder (preferred)
        string? folder = FolderGetter(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        if (!string.IsNullOrWhiteSpace(folder))
            return Path.Combine(folder, "Synentra", "models", DefaultModelFileName);

        // 2. Fall back to a hidden folder inside the user’s home directory
        string? home = FolderGetter(Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.None);
        if (!string.IsNullOrWhiteSpace(home))
        {
            string privateFolder = Path.Combine(home, ".synentra", "models");
            return Path.Combine(privateFolder, DefaultModelFileName);
        }

        // 3. Neither typical safe location is available – throw with guidance
        throw new InvalidOperationException(
            "Unable to determine a secure, user‑specific folder for the community model. " +
            "Please set 'PackagePath' manually in the Semantic configuration.");
    }
}