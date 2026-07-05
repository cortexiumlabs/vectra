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
            TokenIssuance = new TokenIssuanceConfiguration
            {
                Expiration = TimeSpan.FromMinutes(15)
            },
            ExternalIdentity = new ExternalIdentityConfiguration
            {
                Provider = ExternalIdentityProviderType.Jwt,
                Jwt = new JwtIdentityConfiguration
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    Authority = "https://auth.example.com",
                    Audience = "api://default"
                }
            },
            UseCustomHeader = true,
            CustomHeaderName = "X-Custom-Auth",
            FallbackToAuthorization = true
        };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.ExternalIdentity.Provider.Should().Be(ExternalIdentityProviderType.Jwt);
        provider.ExternalIdentity.Jwt.ValidateIssuer.Should().BeTrue();
        provider.ExternalIdentity.Jwt.ValidateAudience.Should().BeTrue();
        provider.ExternalIdentity.Jwt.Authority.Should().Be("https://auth.example.com");
        provider.ExternalIdentity.Jwt.Audience.Should().Be("api://default");
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
        provider.ExternalIdentity.Provider.Should().Be(default(ExternalIdentityProviderType));
        provider.ExternalIdentity.Jwt.ValidateIssuer.Should().BeFalse();
        provider.ExternalIdentity.Jwt.ValidateAudience.Should().BeFalse();
        provider.ExternalIdentity.Jwt.Authority.Should().BeEmpty();
        provider.ExternalIdentity.Jwt.Audience.Should().BeEmpty();
        provider.UseCustomHeader.Should().BeTrue();
        provider.CustomHeaderName.Should().Be("Synentra-Authorization");
        provider.FallbackToAuthorization.Should().BeFalse();
    }

    [Fact]
    public void JwtSection_WhenNull_ShouldReturnDefaultValues()
    {
        // Arrange
        var config = new AgentAuthConfiguration { ExternalIdentity = new ExternalIdentityConfiguration { Jwt = null } };
        var options = CreateOptions(config);

        // Act
        var provider = new AgentAuthConfigProvider(options);

        // Assert
        provider.ExternalIdentity.Jwt.ValidateIssuer.Should().BeFalse();
        provider.ExternalIdentity.Jwt.ValidateAudience.Should().BeFalse();
        provider.ExternalIdentity.Jwt.Authority.Should().BeEmpty();
        provider.ExternalIdentity.Jwt.Audience.Should().BeEmpty();
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