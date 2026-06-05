using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Vectra.UnitTests.Middleware;

public class RequestLoggingMiddlewareTests
{
    [Fact]
    public void Constructor_NullNext_ThrowsArgumentNullException()
    {
        var logger = Substitute.For<ILogger<Vectra.Middleware.RequestLoggingMiddleware>>();
        var version = Substitute.For<Vectra.Application.Abstractions.Versioning.IVersion>();

        var act = () => new Vectra.Middleware.RequestLoggingMiddleware(null!, logger, version);
        act.Should().Throw<ArgumentNullException>().WithParameterName("next");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var version = Substitute.For<Vectra.Application.Abstractions.Versioning.IVersion>();
        var act = () => new Vectra.Middleware.RequestLoggingMiddleware(_ => Task.CompletedTask, null!, version);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullVersion_ThrowsArgumentNullException()
    {
        var logger = Substitute.For<ILogger<Vectra.Middleware.RequestLoggingMiddleware>>();
        var act = () => new Vectra.Middleware.RequestLoggingMiddleware(_ => Task.CompletedTask, logger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("version");
    }

    [Fact]
    public async Task Invoke_NextSucceeds_SetsHeaderAndLogsInformation()
    {
        var logger = Substitute.For<ILogger<Vectra.Middleware.RequestLoggingMiddleware>>();
        var version = Substitute.For<Vectra.Application.Abstractions.Versioning.IVersion>();
        version.Version.Returns(new Version(1, 2));

        var middleware = new Vectra.Middleware.RequestLoggingMiddleware(_ => Task.CompletedTask, logger, version);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/test";
        context.Request.Headers["User-Agent"] = "unit-test-agent";
        context.Items["AgentId"] = Guid.Parse("00000000-0000-0000-0000-000000000001");
        context.Items["Intent"] = "run-job";
        context.Items["RiskScore"] = 0.82;
        context.Items["Decision"] = "hitl";
        context.Items["PolicyVersion"] = "v1.2";

        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Request-Id"].ToString().Should().Be(context.TraceIdentifier);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Invoke_NextThrows_LogsAndPropagatesException()
    {
        var logger = Substitute.For<ILogger<Vectra.Middleware.RequestLoggingMiddleware>>();
        var version = Substitute.For<Vectra.Application.Abstractions.Versioning.IVersion>();
        version.Version.Returns(new Version(1, 2));

        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new Vectra.Middleware.RequestLoggingMiddleware(next, logger, version);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
