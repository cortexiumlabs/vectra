using FluentAssertions;
using Microsoft.Extensions.Options;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.Infrastructure.Security;

namespace Synentra.Infrastructure.UnitTests.Security;

public class AgentAuthConfigProviderTests
{
    private static IOptions<AgentAuthConfiguration> CreateOptions(AgentAuthConfiguration config) =>
        Options.Create(config);

    [Fact]
    public void Constructor_WithValidOptions_ShouldMapAllPropertiesCorrectly()
    {
        // Arrange
        var config = new AgentAuthConfiguration
        {
            Provider = AgentAuthProviderType.Jwt,
            Jwt = new JwtProvider
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                Authority = "https://auth.example.com",
                Audience = "api://default"
            },
            UseCustomHeader = true,
            CustomHeaderName = "X-Custom-Auth",
            FallbackToAuthorization = true
        };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.Provider.Should().Be(AgentAuthProviderType.Jwt);
        provider.ValidateIssuer.Should().BeTrue();
        provider.ValidateAudience.Should().BeTrue();
        provider.Authority.Should().Be("https://auth.example.com");
        provider.Audience.Should().Be("api://default");
        provider.UseCustomHeader.Should().BeTrue();
        provider.CustomHeaderName.Should().Be("X-Custom-Auth");
        provider.FallbackToAuthorization.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldUseDefaultConfiguration()
    {
        // Act
        var provider = new AgentAuthConfigProvider(null!);

        // Assert
        provider.Provider.Should().Be(default(AgentAuthProviderType));
        provider.ValidateIssuer.Should().BeFalse();
        provider.ValidateAudience.Should().BeFalse();
        provider.Authority.Should().BeEmpty();
        provider.Audience.Should().BeEmpty();
        provider.UseCustomHeader.Should().BeTrue();
        provider.CustomHeaderName.Should().Be("Synentra-Authorization");
        provider.FallbackToAuthorization.Should().BeFalse();
    }

    [Fact]
    public void JwtSection_WhenNull_ShouldReturnDefaultValues()
    {
        // Arrange
        var config = new AgentAuthConfiguration { Jwt = null };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.ValidateIssuer.Should().BeFalse();
        provider.ValidateAudience.Should().BeFalse();
        provider.Authority.Should().BeNull();
        provider.Audience.Should().BeNull();
    }

    [Fact]
    public void CustomHeaderName_WhenNull_ShouldReturnDefaultHeaderName()
    {
        // Arrange
        var config = new AgentAuthConfiguration { CustomHeaderName = null };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.CustomHeaderName.Should().Be("Synentra-Authorization");
    }

    [Fact]
    public void CustomHeaderName_WhenSet_ShouldReturnConfiguredName()
    {
        // Arrange
        var config = new AgentAuthConfiguration { CustomHeaderName = "X-My-Header" };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.CustomHeaderName.Should().Be("X-My-Header");
    }

    [Fact]
    public void UseCustomHeader_ShouldBeMappedDirectly()
    {
        // Arrange
        var config = new AgentAuthConfiguration { UseCustomHeader = true };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.UseCustomHeader.Should().BeTrue();
    }

    [Fact]
    public void FallbackToAuthorization_ShouldBeMappedDirectly()
    {
        // Arrange
        var config = new AgentAuthConfiguration { FallbackToAuthorization = true };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.FallbackToAuthorization.Should().BeTrue();
    }
}