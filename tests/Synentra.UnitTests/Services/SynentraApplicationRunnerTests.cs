using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Synentra.Services;

namespace Synentra.UnitTests.Services;

public class SynentraApplicationRunnerTests
{
    [Fact]
    public async Task Run_Should_Render_Splash_And_Start()
    {
        var splash = Substitute.For<ISplashScreen>();
        var startup = Substitute.For<IStartupConfiguration>();
        var factory = Substitute.For<IWebApplicationFactory>();

        var builder = WebApplication.CreateBuilder();

        factory.Create(Arg.Any<string[]>()).Returns(builder);

        var runner = new SynentraApplicationRunner(factory, splash, startup);

        startup.When(x => x.ConfigurePipelineAsync(Arg.Any<WebApplication>()))
            .Do(_ => throw new OperationCanceledException());

        try
        {
            await runner.RunAsync([], CancellationToken.None);
        }
        catch { }

        splash.Received(1).Render();
        startup.Received(1).ConfigureServices(builder);
    }

    [Fact]
    public async Task HandleStartupFailure_Should_Exit_With_1()
    {
        var builder = WebApplication.CreateBuilder();

        var logger = Substitute.For<ILogger<Program>>();
        builder.Services.AddSingleton(logger);

        int capturedExitCode = -1;
        SynentraApplicationRunner.ExitAction = code => capturedExitCode = code;

        var ex = new Exception("boom");
        await SynentraApplicationRunner.HandleStartupFailureAsync(builder, ex);

        // Verify the exit code
        capturedExitCode.Should().Be(1);

        // Verify that a critical log was written with the exception
        logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("Unhandled exception during application startup")),
            ex,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task HandleStartupFailure_Should_Write_To_Console_When_Logger_Fails()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<ILogger<Program>>(_ =>
            throw new Exception("fail"));

        var writer = new StringWriter();
        var original = Console.Error;

        Console.SetError(writer);

        try
        {
            SynentraApplicationRunner.ExitAction = _ => { };

            await SynentraApplicationRunner.HandleStartupFailureAsync(
                builder,
                new Exception("kaboom"));

            writer.ToString().Should().Contain("Startup error: kaboom");
        }
        finally
        {
            Console.SetError(original);
        }
    }
}