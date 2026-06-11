using FluentAssertions;
using Synentra.BuildingBlocks.Errors;
using Synentra.BuildingBlocks.Results;
using Xunit;

namespace Synentra.BuildingBlocks.UnitTests.Errors;

public class ErrorTests
{
    [Fact]
    public void Validation_ShouldCreateErrorWithValidationType()
    {
        var validationErrors = new Dictionary<string, string[]> { { "Name", ["Name is required"] } };

        var error = Error.Validation(SynentraErrors.ValidationFailed, "Validation failed", validationErrors);

        error.Type.Should().Be(ErrorType.Validation);
        error.ErrorCode.Should().Be(SynentraErrors.ValidationFailed);
        error.Message.Should().Be("Validation failed");
        error.ValidationErrors.Should().BeEquivalentTo(validationErrors);
    }

    [Fact]
    public void NotFound_ShouldCreateErrorWithNotFoundType()
    {
        var error = Error.NotFound(SynentraErrors.ResourceNotFound, "Resource not found");

        error.Type.Should().Be(ErrorType.NotFound);
        error.ErrorCode.Should().Be(SynentraErrors.ResourceNotFound);
        error.Message.Should().Be("Resource not found");
        error.ValidationErrors.Should().BeNull();
    }

    [Fact]
    public void Conflict_ShouldCreateErrorWithConflictType()
    {
        var error = Error.Conflict(SynentraErrors.DuplicateResource, "Duplicate resource");

        error.Type.Should().Be(ErrorType.Conflict);
        error.Message.Should().Be("Duplicate resource");
    }

    [Fact]
    public void Unauthorized_ShouldCreateErrorWithUnauthorizedType()
    {
        var error = Error.Unauthorized(SynentraErrors.Unauthorized, "Unauthorized access");

        error.Type.Should().Be(ErrorType.Unauthorized);
        error.Message.Should().Be("Unauthorized access");
    }

    [Fact]
    public void Forbidden_ShouldCreateErrorWithForbiddenType()
    {
        var error = Error.Forbidden(SynentraErrors.AccessDenied, "Access denied");

        error.Type.Should().Be(ErrorType.Forbidden);
        error.Message.Should().Be("Access denied");
    }

    [Fact]
    public void Failure_ShouldCreateErrorWithFailureType()
    {
        var error = Error.Failure(SynentraErrors.SystemFailure, "System failure");

        error.Type.Should().Be(ErrorType.Failure);
        error.Message.Should().Be("System failure");
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        var error = Error.Failure(SynentraErrors.SystemFailure, "System failure");

        error.ToString().Should().Be($"{SynentraErrors.SystemFailure}: System failure");
    }

    [Theory]
    [InlineData(400000, ErrorCategory.Core, ErrorType.Validation)]
    [InlineData(404000, ErrorCategory.Persistence, ErrorType.NotFound)]
    [InlineData(409000, ErrorCategory.Persistence, ErrorType.Conflict)]
    [InlineData(401000, ErrorCategory.Security, ErrorType.Unauthorized)]
    [InlineData(403000, ErrorCategory.Security, ErrorType.Forbidden)]
    [InlineData(1, ErrorCategory.System, ErrorType.Failure)]
    public void FromCode_ShouldMapErrorCodeToCorrectErrorType(int codeValue, ErrorCategory category, ErrorType expectedType)
    {
        var errorCode = new ErrorCode(codeValue, category);

        var error = Error.FromCode(errorCode, "test message");

        error.Type.Should().Be(expectedType);
    }

    [Fact]
    public void FromCode_WithValidationErrors_ShouldIncludeValidationErrors()
    {
        var validationErrors = new Dictionary<string, string[]> { { "Field", ["Required"] } };
        var errorCode = new ErrorCode(400000, ErrorCategory.Core);

        var error = Error.FromCode(errorCode, "Validation failed", validationErrors);

        error.ValidationErrors.Should().BeEquivalentTo(validationErrors);
    }
}
