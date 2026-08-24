namespace Synentra.Services;

internal class SplashScreen: ISplashScreen
{
    public void Render()
    {
        var version = SynentraVersion.GetApplicationVersion();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("SYNENTRA");
        Console.WriteLine("Intent-Aware Governance for Autonomous AI Agents");
        Console.WriteLine($"v{version} | https://synentra.io");
        Console.WriteLine("------------------------------------------------");
        Console.ResetColor();
    }
}