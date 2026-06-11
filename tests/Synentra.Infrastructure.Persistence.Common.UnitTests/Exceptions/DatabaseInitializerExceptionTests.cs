using FluentAssertions;
using Synentra.Infrastructure.Persistence.Common.Errors;
using Synentra.Infrastructure.Persistence.Common.Exceptions;

namespace Synentra.Infrastructure.Persistence.Common.UnitTests.Exceptions;

public class DatabaseInitializerExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetInnerException()
    {
        var inner = new InvalidOperationException("connect error");

        var ex = new DatabaseInitializerException(inner);

        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void Constructor_ShouldSetCorrectErrorCode()
    {
        var ex = new DatabaseInitializerException(new Exception("fail"));

        ex.ErrorCode.Should().Be(PersistenceErrorCodes.DatabaseInitializer);
    }

    [Fact]
    public void Constructor_ShouldIncludeInnerMessageInMessage()
    {
        var inner = new Exception("connection refused");

        var ex = new DatabaseInitializerException(inner);

        ex.Message.Should().Contain("connection refused");
    }

    [Fact]
    public void Constructor_ShouldContainContextualPrefix()
    {
        var ex = new DatabaseInitializerException(new Exception("x"));

        ex.Message.Should().Contain("Error occurred while connecting the application database");
    }

    [Fact]
    public void ShouldBe_PersistenceException()
    {
        var ex = new DatabaseInitializerException(new Exception());

        ex.Should().BeAssignableTo<PersistenceException>();
    }
}
