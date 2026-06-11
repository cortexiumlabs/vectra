using System.CommandLine;
using Vectra.Services;

namespace Vectra.Commands;

internal static class VectraCommandLine
{
    internal static Func<IVectraApplicationRunner> RunnerFactory = null!;

    static VectraCommandLine()
    {
        RunnerFactory = () =>
            new VectraApplicationRunner(
                new DefaultWebApplicationFactory(),
                new SplashScreen(),
                new StartupConfiguration());
    }

    public static RootCommand Create(string[] args)
    {
        var versionOption = new Option<bool>("--version", "-v")
        {
            Description = "Show the current Vectra version."
        };

        var rootCommand = new RootCommand(
            "VECTRA – Intent-Aware Governance Gateway for Autonomous AI Agents");

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
                    $"Vectra {VectraVersion.GetApplicationVersion()}");

                return;
            }

            var runner = RunnerFactory();

            await runner.RunAsync(args, cancellationToken);
        });

        return rootCommand;
    }
}