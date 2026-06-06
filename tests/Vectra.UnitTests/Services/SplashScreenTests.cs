using Vectra.Services;

namespace Vectra.UnitTests.Services;

public class SplashScreenTests
{
    [Fact]
    public void Render_DoesNotThrow()
    {
        // Capture console output to avoid polluting test output
        var originalOut = Console.Out;
        var originalColor = Console.ForegroundColor;

        try
        {
            Console.SetOut(TextWriter.Null);
            var splash = Substitute.For<ISplashScreen>();
            Action act = () => splash.Render();
            act.Should().NotThrow();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.ForegroundColor = originalColor;
        }
    }

    [Fact]
    public void Render_WritesContent()
    {
        var writer = new StringWriter();
        var originalOut = Console.Out;
        var originalColor = Console.ForegroundColor;

        try
        {
            Console.SetOut(writer);
            var splash = new SplashScreen();
            splash.Render();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.ForegroundColor = originalColor;
        }

        var output = writer.ToString();
        output.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Render_OutputContainsCortexiumBranding()
    {
        var writer = new StringWriter();
        var originalOut = Console.Out;
        var originalColor = Console.ForegroundColor;

        try
        {
            Console.SetOut(writer);
            var splash = new SplashScreen();
            splash.Render();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.ForegroundColor = originalColor;
        }

        var output = writer.ToString();
        output.Should().ContainAny("Cortexium", "cortexiumlabs", "vectra", "Vectra");
    }
}
