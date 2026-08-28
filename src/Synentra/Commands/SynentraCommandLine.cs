using System.CommandLine;
using Synentra.Services;

namespace Synentra.Commands;

internal static class SynentraCommandLine
{
    internal static Func<ISynentraApplicationRunner> RunnerFactory { get; set; } = null!;

    static SynentraCommandLine()
    {
        RunnerFactory = () =>
            new SynentraApplicationRunner(
                new DefaultWebApplicationFactory(),
                new SplashScreen(),
                new StartupConfiguration());
    }

    public static RootCommand Create(string[] args)
    {
        var versionOption = new Option<bool>("--version", "-v")
        {
            Description = "Show the current Synentra version."
        };

        var rootCommand = new RootCommand(
            "SYNENTRA – Intent-Aware Governance Gateway for Autonomous AI Agents");

        var builtIn =
            rootCommand.Options.OfType<VersionOption>().FirstOrDefault();

        if (builtIn is not null)
            rootCommand.Options.Remove(builtIn);

        rootCommand.Options.Add(versionOption);

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            if (parseResult.GetValue(versionOption))
            {
                Console.WriteLine(
                    $"Synentra {SynentraVersion.GetApplicationVersion()}");

                return;
            }

            var runner = RunnerFactory();

            await runner.RunAsync(args, cancellationToken);
        });

        return rootCommand;
    }
}