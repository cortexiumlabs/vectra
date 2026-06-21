using Synentra.Commands;
using Synentra.Services;

namespace Synentra.UnitTests.Commands;

public class SynentraCommandLineTests
{
    [Fact]
    public async Task Version_Should_Print_And_Not_Run()
    {
        var runner = Substitute.For<ISynentraApplicationRunner>();

        SynentraCommandLine.RunnerFactory = () => runner;

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);

        try
        {
            var cmd = SynentraCommandLine.Create([]);

            await cmd.Parse("--version").InvokeAsync();

            Assert.Contains("Synentra", output.ToString());

            await runner.DidNotReceive()
                .RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public async Task Default_Should_Run_Runner()
    {
        var runner = Substitute.For<ISynentraApplicationRunner>();

        SynentraCommandLine.RunnerFactory = () => runner;

        var args = new[] { "run", "app" };

        var cmd = SynentraCommandLine.Create(args);

        await cmd.Parse("").InvokeAsync();

        await runner.Received(1)
            .RunAsync(args, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShortVersion_Should_Not_Run()
    {
        var runner = Substitute.For<ISynentraApplicationRunner>();

        SynentraCommandLine.RunnerFactory = () => runner;

        var cmd = SynentraCommandLine.Create([]);

        await cmd.Parse("-v").InvokeAsync();

        await runner.DidNotReceive()
            .RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }
}