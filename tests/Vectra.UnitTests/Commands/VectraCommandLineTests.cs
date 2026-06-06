using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Vectra.Commands;

namespace Vectra.UnitTests.Commands;

public class VectraCommandLineTests
{
    [Fact]
    public void Create_ReturnsRootCommand()
    {
        var rootCommand = VectraCommandLine.Create([]);
        rootCommand.Should().NotBeNull().And.BeOfType<RootCommand>();
    }

    [Fact]
    public void Create_RootCommandHasVersionOption()
    {
        var rootCommand = VectraCommandLine.Create([]);
        // Options can be matched by any alias; check both long and short forms
        var hasVersion = rootCommand.Options.Any(o =>
            o.Name == "version" || o.Aliases.Contains("--version") || o.Aliases.Contains("-v"));
        hasVersion.Should().BeTrue();
    }

    [Fact]
    public void Create_BuiltInVersionOptionRemoved()
    {
        var rootCommand = VectraCommandLine.Create([]);
        // Confirm the custom --version option is present (exactly one option covering --version)
        var versionOptions = rootCommand.Options
            .Where(o => o.Name == "version" || o.Aliases.Contains("--version") || o.Aliases.Contains("-v"))
            .ToList();
        versionOptions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_VersionFlag_PrintsVersionAndReturns0()
    {
        var rootCommand = VectraCommandLine.Create([]);

        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        int exitCode;
        try
        {
            var parseResult = rootCommand.Parse(["--version"]);
            exitCode = await parseResult.InvokeAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        exitCode.Should().Be(0);
        var output = writer.ToString();
        output.Should().Contain("Vectra");
    }

    [Fact]
    public void Create_WithArgs_DoesNotThrow()
    {
        var act = () => VectraCommandLine.Create(["--version"]);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task HandleStartupFailureAsync_LogsCriticalAndExits()
    {
        // Arrange
        var exitCode = -1;
        VectraCommandLine.ExitAction = code => exitCode = code;

        var builder = WebApplication.CreateBuilder([]);
        var logger = Substitute.For<ILogger<Program>>();
        builder.Services.AddSingleton(logger);
        var ex = new InvalidOperationException("Test startup failure");

        // Act
        await VectraCommandLine.HandleStartupFailureAsync(builder, ex);

        // Assert
        logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Unhandled exception during application startup")),
            ex,
            Arg.Any<Func<object, Exception?, string>>());
        exitCode.Should().Be(1);

        // Cleanup
        VectraCommandLine.ExitAction = Environment.Exit;
    }

    [Fact]
    public async Task HandleStartupFailureAsync_WhenLoggerFails_WritesToConsoleAndExits()
    {
        // Arrange
        var exitCode = -1;
        VectraCommandLine.ExitAction = code => exitCode = code;

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<Program>>(sp => throw new InvalidOperationException("Logger failed"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        builder.Services.AddSingleton<ILogger<Program>>(sp => throw new InvalidOperationException("Logger failed"));


        var ex = new InvalidOperationException("Test startup failure");

        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);

        // Act
        await VectraCommandLine.HandleStartupFailureAsync(builder, ex);

        // Assert
        exitCode.Should().Be(1);
        var output = writer.ToString();
        output.Should().Contain($"Startup error: {ex.Message}");

        // Cleanup
        VectraCommandLine.ExitAction = Environment.Exit;
        Console.SetError(originalError);
    }

    [Fact]
    public async Task Create_NoArgs_InvokesRun()
    {
        var rootCommand = VectraCommandLine.Create([]);

        // We expect the handler to throw because we can't fully mock the WebApplication startup.
        // The key is to verify that it *tries* to start, which means it will fail at a certain point.
        // This confirms the action handler is invoked when no --version flag is present.
        var act = async () => await rootCommand.Parse([]).InvokeAsync();

        // The specific exception may vary, but it should be related to configuration or host building.
        await act.Should().ThrowAsync<Exception>();
    }
}
