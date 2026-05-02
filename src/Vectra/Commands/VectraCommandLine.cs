using System.CommandLine;
using Vectra.Configuration;
using Vectra.Extensions;
using Vectra.Services;
using Vectra.Utilities;

namespace Vectra.Commands;

internal static class VectraCommandLine
{
    public static RootCommand Create(string[] args)
    {
        var versionOption = new Option<bool>("--version", "-v")
        {
            Description = "Show the current Vectra version."
        };

        var rootCommand = new RootCommand("VECTRA – Intent-Aware Governance Gateway for Autonomous AI Agents");

        // Replace the built-in VersionOption with our own so the output uses VectraVersion.
        var builtIn = rootCommand.Options.OfType<VersionOption>().FirstOrDefault();
        if (builtIn is not null)
            rootCommand.Options.Remove(builtIn);

        rootCommand.Options.Add(versionOption);

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            if (parseResult.GetValue(versionOption))
            {
                Console.WriteLine($"Vectra {VectraVersion.GetApplicationVersion()}");
                return;
            }

            SplashScreen.Render();

            var builder = WebApplication.CreateBuilder(args);

            try
            {
                builder.AddVectraSecretManagement();

                StartupConfiguration.ConfigureServices(builder);

                var app = builder.Build();
                await StartupConfiguration.ConfigurePipelineAsync(app);

                await app.RunAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await HandleStartupFailureAsync(builder, ex);
            }
        });

        return rootCommand;
    }

    private static async Task HandleStartupFailureAsync(WebApplicationBuilder builder, Exception ex)
    {
        try
        {
            using var scope = builder.Services.BuildServiceProvider().CreateScope();
            var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
            logger?.LogCritical(ex, "Unhandled exception during application startup");
        }
        catch
        {
            await Console.Error.WriteLineAsync($"Startup error: {ex.Message}");
        }

        await Task.Delay(500);
        Environment.Exit(1);
    }
}