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

    [Fact]
    public void TokenIssuance_WhenConfigured_ShouldMapAllProperties()
    {
        var config = new AgentAuthConfiguration
        {
            TokenIssuance = new TokenIssuanceConfiguration
            {
                Issuer = "test-issuer",
                Audience = "test-audience",
                Secret = "super-secret",
                Expiration = TimeSpan.FromMinutes(30)
            }
        };
        var provider = new AgentAuthConfigProvider(CreateOptions(config));

        provider.TokenIssuance.Issuer.Should().Be("test-issuer");
        provider.TokenIssuance.Audience.Should().Be("test-audience");
        provider.TokenIssuance.Secret.Should().Be("super-secret");
        provider.TokenIssuance.Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void TokenIssuance_WhenNull_ShouldReturnDefaults()
    {
        var config = new AgentAuthConfiguration { TokenIssuance = null };
        var provider = new AgentAuthConfigProvider(CreateOptions(config));

        provider.TokenIssuance.Issuer.Should().BeEmpty();
        provider.TokenIssuance.Audience.Should().BeEmpty();
        provider.TokenIssuance.Secret.Should().BeEmpty();
        provider.TokenIssuance.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void TokenIssuance_WhenNullOptions_ShouldReturnDefaults()
    {
        var provider = new AgentAuthConfigProvider(null!);

        provider.TokenIssuance.Issuer.Should().BeEmpty();
        provider.TokenIssuance.Audience.Should().BeEmpty();
        provider.TokenIssuance.Secret.Should().BeEmpty();
        provider.TokenIssuance.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ExternalIdentity_WhenNull_ShouldUseJwtProviderAndDefaults()
    {
        var config = new AgentAuthConfiguration { ExternalIdentity = null };
        var provider = new AgentAuthConfigProvider(CreateOptions(config));

        provider.ExternalIdentity.Provider.Should().Be(ExternalIdentityProviderType.Jwt);
        provider.ExternalIdentity.Jwt.Authority.Should().BeEmpty();
        provider.ExternalIdentity.Jwt.Audience.Should().BeEmpty();
        provider.ExternalIdentity.Jwt.ValidateIssuer.Should().BeFalse();
        provider.ExternalIdentity.Jwt.ValidateAudience.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithOptionsValueNull_ShouldUseDefaultConfiguration()
    {
        var options = new NullValueOptions(); // Value returns null

        var provider = new AgentAuthConfigProvider(options);

        // Same defaults as when the whole options object is null
        provider.UseCustomHeader.Should().BeTrue();                // default of AgentAuthConfiguration
        provider.CustomHeaderName.Should().Be("Synentra-Authorization");
        provider.FallbackToAuthorization.Should().BeFalse();
        provider.TokenIssuance.Expiration.Should().Be(TimeSpan.FromMinutes(15));
        provider.ExternalIdentity.Provider.Should().Be(ExternalIdentityProviderType.Jwt);
        provider.ExternalIdentity.Jwt.Authority.Should().BeEmpty();
    }

    private sealed class NullValueOptions : IOptions<AgentAuthConfiguration>
    {
        public AgentAuthConfiguration Value => null!;
    }
}