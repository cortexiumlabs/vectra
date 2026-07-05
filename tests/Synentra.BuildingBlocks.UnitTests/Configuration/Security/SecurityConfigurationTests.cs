using FluentAssertions;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Xunit;

namespace Synentra.BuildingBlocks.UnitTests.Configuration.Security;

public class SecurityConfigurationTests
{
    [Fact]
    public void DefaultValues_ShouldInitializeAgentAuth()
    {
        var config = new SecurityConfiguration();

        config.AgentAuth.Should().NotBeNull();
    }

    [Fact]
    public void AgentAuthConfiguration_DefaultValues_ShouldBeCorrect()
    {
        var config = new AgentAuthConfiguration();

        config.ExternalIdentity.Provider.Should().Be(ExternalIdentityProviderType.Jwt);
        config.TokenIssuance.Should().NotBeNull();
        config.ExternalIdentity.Should().NotBeNull();
        config.UseCustomHeader.Should().BeTrue();
        config.CustomHeaderName.Should().Be("Synentra-Authorization");
        config.FallbackToAuthorization.Should().BeFalse();
    }

    [Fact]
    public void AgentAuthConfiguration_ShouldAllowCustomValues()
    {
        var config = new AgentAuthConfiguration
        {
            UseCustomHeader = true,
            CustomHeaderName = "X-Custom-Auth",
            FallbackToAuthorization = true
        };

        config.ExternalIdentity.Provider.Should().Be(ExternalIdentityProviderType.Jwt);
        config.TokenIssuance.Should().NotBeNull();
        config.ExternalIdentity.Should().NotBeNull();
        config.UseCustomHeader.Should().BeTrue();
        config.CustomHeaderName.Should().Be("X-Custom-Auth");
        config.FallbackToAuthorization.Should().BeTrue();
    }

    [Fact]
    public void SelfSignedProvider_DefaultValues_ShouldBeCorrect()
    {
        var config = new TokenIssuanceConfiguration();

        config.Secret.Should().BeEmpty();
        config.Issuer.Should().BeEmpty();
        config.Audience.Should().BeEmpty();
        config.Expiration.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void SelfSignedProvider_ShouldAllowCustomValues()
    {
        var config = new TokenIssuanceConfiguration
        {
            Secret = "my-secret",
            Issuer = "my-issuer",
            Audience = "my-audience",
            Expiration = TimeSpan.FromHours(1)
        };

        config.Secret.Should().Be("my-secret");
        config.Issuer.Should().Be("my-issuer");
        config.Audience.Should().Be("my-audience");
        config.Expiration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void JwtProvider_DefaultValues_ShouldBeCorrect()
    {
        var config = new JwtIdentityConfiguration();

        config.Authority.Should().BeEmpty();
        config.Audience.Should().BeEmpty();
        config.MetadataUrl.Should().BeEmpty();
        config.ValidateIssuer.Should().BeFalse();
        config.ValidateAudience.Should().BeFalse();
    }

    [Fact]
    public void JwtProvider_ShouldAllowCustomValues()
    {
        var config = new JwtIdentityConfiguration
        {
            Authority = "https://auth.example.com",
            Audience = "api",
            MetadataUrl = "https://auth.example.com/.well-known/openid-configuration",
            ValidateIssuer = false,
            ValidateAudience = false
        };

        config.Authority.Should().Be("https://auth.example.com");
        config.Audience.Should().Be("api");
        config.ValidateIssuer.Should().BeFalse();
        config.ValidateAudience.Should().BeFalse();
    }

    [Theory]
    [InlineData(ExternalIdentityProviderType.Jwt)]
    public void ExternalIdentityProviderType_AllValues_ShouldBeDefined(ExternalIdentityProviderType providerType)
    {
        Enum.IsDefined(providerType).Should().BeTrue();
    }
}
