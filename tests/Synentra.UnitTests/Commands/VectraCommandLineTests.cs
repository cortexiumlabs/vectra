using Vectra.Commands;
using Vectra.Services;

namespace Vectra.UnitTests.Commands;

public class VectraCommandLineTests
{
    [Fact]
    public async Task Version_Should_Print_And_Not_Run()
    {
        var runner = Substitute.For<IVectraApplicationRunner>();

        VectraCommandLine.RunnerFactory = () => runner;

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);

        try
        {
            var cmd = VectraCommandLine.Create([]);

            await cmd.Parse("--version").InvokeAsync();

            Assert.Contains("Vectra", output.ToString());

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
        var runner = Substitute.For<IVectraApplicationRunner>();

        VectraCommandLine.RunnerFactory = () => runner;

        var args = new[] { "run", "app" };

        var cmd = VectraCommandLine.Create(args);

        await cmd.Parse("").InvokeAsync();

        await runner.Received(1)
            .RunAsync(args, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShortVersion_Should_Not_Run()
    {
        var runner = Substitute.For<IVectraApplicationRunner>();

        VectraCommandLine.RunnerFactory = () => runner;

        var cmd = VectraCommandLine.Create([]);

        await cmd.Parse("-v").InvokeAsync();

        await runner.DidNotReceive()
            .RunAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }
}