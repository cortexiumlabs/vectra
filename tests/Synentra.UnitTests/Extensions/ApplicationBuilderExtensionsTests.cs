using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Synentra.Extensions;

namespace Synentra.UnitTests.Extensions;

public class ApplicationBuilderExtensionsTests
{
    // ── UseSynentraCustomException ───────────────────────────────────────────

    [Fact]
    public void UseSynentraCustomException_ReturnsSameBuilder()
    {
        var builder = Substitute.For<IApplicationBuilder>();
        builder.Use(Arg.Any<Func<RequestDelegate, RequestDelegate>>()).Returns(builder);
        builder.New().Returns(builder);

        var result = builder.UseSynentraCustomException();

        result.Should().BeSameAs(builder);
    }

    // ── UseSynentraCustomHeaders ─────────────────────────────────────────────

    [Fact]
    public void UseSynentraCustomHeaders_ReturnsSameBuilder()
    {
        var builder = Substitute.For<IApplicationBuilder>();
        builder.Use(Arg.Any<Func<RequestDelegate, RequestDelegate>>()).Returns(builder);
        builder.New().Returns(builder);

        var result = builder.UseSynentraCustomHeaders();

        result.Should().BeSameAs(builder);
    }
}
