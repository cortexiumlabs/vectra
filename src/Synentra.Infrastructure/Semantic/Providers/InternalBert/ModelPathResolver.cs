namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

public static class ModelPathResolver
{
    private const string DefaultModelFileName = "community-model.zip";

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
        // Use Environment.SpecialFolder.LocalApplicationData to get a writable per‑user directory
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);
        if (string.IsNullOrWhiteSpace(folder))
        {
            // Ultimate fallback – should rarely happen
            folder = Path.GetTempPath();
        }

        return Path.Combine(folder, "Synentra", "models", DefaultModelFileName);
    }
}