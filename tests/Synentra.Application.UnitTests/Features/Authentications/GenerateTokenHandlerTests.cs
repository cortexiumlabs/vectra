using FluentAssertions;
using NSubstitute;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Abstractions.Security;
using Synentra.Application.Errors;
using Synentra.Application.Features.Authentications.GenerateToken;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.Domain.Agents;
using System.Security.Claims;

namespace Synentra.Application.UnitTests.Features.Authentications;

public class GenerateTokenHandlerTests
{
    private readonly IAgentRepository _agentRepository = Substitute.For<IAgentRepository>();
    private readonly IAgentAuthenticator _agentAuthenticator = Substitute.For<IAgentAuthenticator>();
    private readonly ISecretHasher _secretHasher = Substitute.For<ISecretHasher>();
    private readonly IAgentAuthConfigProvider _authConfig = Substitute.For<IAgentAuthConfigProvider>();

    private readonly GenerateTokenHandler _sut;

    public GenerateTokenHandlerTests()
    {
        _authConfig.Provider.Returns(AgentAuthProviderType.SelfSigned);

        _sut = new GenerateTokenHandler(
            _agentRepository,
            _agentAuthenticator,
            _secretHasher,
            _authConfig);
    }

    // Helper to set the agent's Id via reflection (since it's private set)
    private static void SetAgentId(Agent agent, Guid id)
    {
        var property = typeof(Agent).GetProperty("Id");
        property?.SetValue(agent, id);
    }

    // ========== SELF-SIGNED (CLIENT SECRET) TESTS ==========

    [Fact]
    public async Task Handle_ShouldReturnToken_WhenCredentialsAreValid()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId); // Ensure agent Id matches request
        var request = new GenerateTokenRequest { AgentId = agentId, ClientSecret = "plainSecret" };

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        _secretHasher.Verify("plainSecret", agent.ClientSecretHash).Returns(true);
        _agentAuthenticator.Authenticate(agent).Returns(AgentAuthResult.Success("jwt-token"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("jwt-token");
        await _agentAuthenticator.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAgentNotFound()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns((Agent?)null);

        var request = new GenerateTokenRequest { AgentId = agentId, ClientSecret = "secret" };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _agentAuthenticator.DidNotReceive().Authenticate(Arg.Any<Agent>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAgentIsRevoked()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId);
        agent.Revoke();

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);

        var request = new GenerateTokenRequest { AgentId = agentId, ClientSecret = "secret" };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _agentAuthenticator.DidNotReceive().Authenticate(Arg.Any<Agent>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenClientSecretIsInvalid()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId);

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        _secretHasher.Verify("wrongSecret", agent.ClientSecretHash).Returns(false);

        var request = new GenerateTokenRequest { AgentId = agentId, ClientSecret = "wrongSecret" };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _agentAuthenticator.DidNotReceive().Authenticate(Arg.Any<Agent>());
    }

    // ========== EXTERNAL TOKEN (JWT PROVIDER) TESTS ==========

    [Fact]
    public async Task Handle_ShouldReturnToken_WhenExternalTokenIsValid()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId); // <-- FIX: ensure agent Id matches the request and claim
        var externalToken = "valid-external-jwt";
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("sub", agentId.ToString())
            }));

        _authConfig.Provider.Returns(AgentAuthProviderType.Jwt);

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        _agentAuthenticator.ValidateAsync(externalToken, CancellationToken.None).Returns(principal);
        _agentAuthenticator.Authenticate(agent).Returns(AgentAuthResult.Success("jwt-token"));

        var request = new GenerateTokenRequest { AgentId = agentId, ExternalToken = externalToken };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("jwt-token");
        _secretHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenExternalTokenIsInvalid()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId);

        _authConfig.Provider.Returns(AgentAuthProviderType.Jwt);
        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        _agentAuthenticator.ValidateAsync("invalid-token", CancellationToken.None).Returns((System.Security.Claims.ClaimsPrincipal?)null);

        var request = new GenerateTokenRequest { AgentId = agentId, ExternalToken = "invalid-token" };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        _agentAuthenticator.DidNotReceive().Authenticate(Arg.Any<Agent>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenExternalTokenValidationFails()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId);
        var externalToken = "invalid-external-jwt";

        _authConfig.Provider.Returns(AgentAuthProviderType.Jwt);

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        _agentAuthenticator.ValidateAsync(externalToken, CancellationToken.None).Returns((ClaimsPrincipal?)null);

        var request = new GenerateTokenRequest { AgentId = agentId, ExternalToken = externalToken };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.ErrorCode.Should().Be(ApplicationErrorCodes.InvalidClientSession);
        _agentAuthenticator.DidNotReceive().Authenticate(Arg.Any<Agent>());
    }

    [Fact]
    public async Task Handle_ShouldPreferExternalTokenOverClientSecret_WhenBothProvided()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId);
        var externalToken = "valid-external-jwt";
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("sub", agentId.ToString())
            }));

        _authConfig.Provider.Returns(AgentAuthProviderType.Jwt);

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        _agentAuthenticator.ValidateAsync(externalToken, CancellationToken.None).Returns(principal);
        _agentAuthenticator.Authenticate(agent).Returns(AgentAuthResult.Success("jwt-token"));

        var request = new GenerateTokenRequest
        {
            AgentId = agentId,
            ClientSecret = "any-secret",
            ExternalToken = externalToken
        };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _secretHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldFallbackToClientSecret_WhenExternalTokenIsNotProvided()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new Agent("TestAgent", "owner-1", "hashed-secret");
        SetAgentId(agent, agentId);

        _authConfig.Provider.Returns(AgentAuthProviderType.Jwt); // config says Jwt, but agent sends no external token

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        _secretHasher.Verify("plainSecret", agent.ClientSecretHash).Returns(true);
        _agentAuthenticator.Authenticate(agent).Returns(AgentAuthResult.Success("jwt-token"));

        var request = new GenerateTokenRequest { AgentId = agentId, ClientSecret = "plainSecret" };

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _agentAuthenticator.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ========== CONSTRUCTOR GUARD CLAUSES ==========

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryIsNull()
    {
        var act = () => new GenerateTokenHandler(null!, _agentAuthenticator, _secretHasher, _authConfig);
        act.Should().Throw<ArgumentNullException>().WithParameterName("agentRepository");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenAuthenticatorIsNull()
    {
        var act = () => new GenerateTokenHandler(_agentRepository, null!, _secretHasher, _authConfig);
        act.Should().Throw<ArgumentNullException>().WithParameterName("agentAuthenticator");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSecretHasherIsNull()
    {
        var act = () => new GenerateTokenHandler(_agentRepository, _agentAuthenticator, null!, _authConfig);
        act.Should().Throw<ArgumentNullException>().WithParameterName("secretHasher");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenAuthConfigIsNull()
    {
        var act = () => new GenerateTokenHandler(_agentRepository, _agentAuthenticator, _secretHasher, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("authConfig");
    }
}