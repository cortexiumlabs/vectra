using Synentra.Extensions;

namespace Synentra.Services;

internal sealed class SynentraApplicationRunner : ISynentraApplicationRunner
{
    private readonly IWebApplicationFactory _factory;
    private readonly ISplashScreen _splashScreen;
    private readonly IStartupConfiguration _startupConfiguration;

    public SynentraApplicationRunner(
            IWebApplicationFactory factory,
            ISplashScreen splashScreen,
            IStartupConfiguration startupConfiguration)
    {
        _factory = factory;
        _splashScreen = splashScreen;
        _startupConfiguration = startupConfiguration;
    }

    public async Task RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        _splashScreen.Render();

        var builder = _factory.Create(args);

        try
        {
            builder.AddSynentraSecretManagement();

            builder.Logging.AddFilter(
                "Microsoft.AspNetCore.Hosting.Diagnostics",
                LogLevel.Warning);

            _startupConfiguration.ConfigureServices(builder);

            var app = builder.Build();

            await _startupConfiguration.ConfigurePipelineAsync(app);

            await app.RunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await HandleStartupFailureAsync(builder, ex);
        }
    }

    internal static Action<int> ExitAction = Environment.Exit;

    internal static async Task HandleStartupFailureAsync(
        WebApplicationBuilder builder,
        Exception ex)
    {
        try
        {
            using var scope =
                builder.Services.BuildServiceProvider().CreateScope();

            var logger =
                scope.ServiceProvider.GetService<ILogger<Program>>();

            logger?.LogCritical(
                ex,
                "Unhandled exception during application startup");
        }
        catch
        {
            await Console.Error.WriteLineAsync(
                $"Startup error: {ex.Message}");
        }

        await Task.Delay(500);

        ExitAction(1);
    }
}