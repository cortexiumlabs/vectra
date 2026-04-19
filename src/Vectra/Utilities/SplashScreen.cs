using System.Reflection;
using Vectra.Services;

namespace Vectra.Utilities;

internal static class SplashScreen
{
    private const string ResourceName = "Vectra.Resources.splash.txt";

    public static void Render()
    {
        var version = VectraVersion.GetApplicationVersion();
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        using var reader = new StreamReader(stream);
        var splashContent = reader.ReadToEnd();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(splashContent);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"    v{version} | © Cortexium Labs. All rights reserved.");
        Console.WriteLine("    https://cortexiumlabs.com/vectra");
        Console.WriteLine("    ------------------------------------------------");
        Console.WriteLine("    Intent-Aware Governance for Secure, Observable,");
        Console.WriteLine("    and Controlled Interactions Across Autonomous");
        Console.WriteLine("    Agents and Systems.");
        Console.WriteLine();
        Console.ResetColor();
    }
}