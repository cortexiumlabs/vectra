using FluentAssertions;
using Synentra.BuildingBlocks.Errors;
using Xunit;

namespace Synentra.BuildingBlocks.UnitTests.Errors;

public class SynentraErrorsTests
{
    [Fact]
    public void SystemFailure_ShouldHaveSystemCategory()
    {
        SynentraErrors.SystemFailure.Category.Should().Be(ErrorCategory.System);
        SynentraErrors.SystemFailure.Value.Should().Be(1);
    }

    [Fact]
    public void FileNotFound_ShouldHaveInfrastructureCategory()
    {
        SynentraErrors.FileNotFound.Category.Should().Be(ErrorCategory.Infrastructure);
    }

    [Fact]
    public void SerializationErrors_ShouldHaveSerializationCategory()
    {
        SynentraErrors.SerializationFailed.Category.Should().Be(ErrorCategory.Serialization);
        SynentraErrors.DeserializationFailed.Category.Should().Be(ErrorCategory.Serialization);
        SynentraErrors.InvalidJson.Category.Should().Be(ErrorCategory.Serialization);
    }

    [Fact]
    public void Unauthorized_ShouldHaveSecurityCategory()
    {
        SynentraErrors.Unauthorized.Category.Should().Be(ErrorCategory.Security);
    }

    [Fact]
    public void ValidationErrors_ShouldHaveCoreCategory()
    {
        SynentraErrors.ValidationFailed.Category.Should().Be(ErrorCategory.Core);
        SynentraErrors.RequiredFieldMissing.Category.Should().Be(ErrorCategory.Core);
    }

    [Fact]
    public void ResourceNotFound_ShouldHavePersistenceCategory()
    {
        SynentraErrors.ResourceNotFound.Category.Should().Be(ErrorCategory.Persistence);
    }

    [Fact]
    public void DuplicateResource_ShouldHavePersistenceCategory()
    {
        SynentraErrors.DuplicateResource.Category.Should().Be(ErrorCategory.Persistence);
    }

    [Fact]
    public void SecurityErrors_ShouldHaveSecurityCategory()
    {
        SynentraErrors.AccessDenied.Category.Should().Be(ErrorCategory.Security);
        SynentraErrors.MissingCredentials.Category.Should().Be(ErrorCategory.Security);
        SynentraErrors.ExpiredSession.Category.Should().Be(ErrorCategory.Security);
    }

    [Fact]
    public void AllErrorCodes_ShouldHaveUniqueValues()
    {
        var codes = new[]
        {
            SynentraErrors.SystemFailure.Value,
            SynentraErrors.FileNotFound.Value,
            SynentraErrors.SerializationFailed.Value,
            SynentraErrors.DeserializationFailed.Value,
            SynentraErrors.InvalidJson.Value,
            SynentraErrors.Unauthorized.Value,
            SynentraErrors.ValidationFailed.Value,
            SynentraErrors.RequiredFieldMissing.Value,
            SynentraErrors.ResourceNotFound.Value,
            SynentraErrors.DuplicateResource.Value,
            SynentraErrors.AccessDenied.Value,
            SynentraErrors.MissingCredentials.Value,
            SynentraErrors.ExpiredSession.Value
        };

        codes.Should().OnlyHaveUniqueItems();
    }
}
