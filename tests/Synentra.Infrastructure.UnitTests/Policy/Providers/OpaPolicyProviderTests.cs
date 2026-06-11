using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.Policy;
using Vectra.Domain.Policies;
using Vectra.Infrastructure.Policy.Providers;

namespace Vectra.Infrastructure.UnitTests.Policy.Providers;

public class OpaPolicyProviderTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IOptions<PolicyConfiguration> _policyConfiguration = Substitute.For<IOptions<PolicyConfiguration>>();
    private readonly ILogger<OpaPolicyProvider> _logger = Substitute.For<ILogger<OpaPolicyProvider>>();

    private OpaPolicyProvider CreateProviderWithMockHttp(HttpResponseMessage responseMessage)
    {
        _policyConfiguration.Value.Returns(new PolicyConfiguration { Providers = new PolicyProviders { Opa = new OpaPolicyConfiguration { BaseUrl = "http://localhost" } } });

        var mockMessageHandler = new MockHttpMessageHandler(responseMessage);
        var httpClient = new HttpClient(mockMessageHandler);
        _httpClientFactory.CreateClient("opa-policy").Returns(httpClient);

        return new OpaPolicyProvider(_httpClientFactory, _policyConfiguration, _logger);
    }

    [Fact]
    public async Task EvaluateAsync_OpaUrlNotConfigured_ShouldReturnDeny()
    {
        // Arrange
        _policyConfiguration.Value.Returns(new PolicyConfiguration { Providers = new PolicyProviders { Opa = new OpaPolicyConfiguration { BaseUrl = string.Empty } } });
        var provider = new OpaPolicyProvider(_httpClientFactory, _policyConfiguration, _logger);
        var input = new Dictionary<string, object>();

        // Act
        var decision = await provider.EvaluateAsync("test-policy", input, CancellationToken.None);

        // Assert
        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Be("OPA is selected but OPA base URL is not configured");
    }

    [Fact]
    public async Task EvaluateAsync_OpaAllows_ShouldReturnAllow()
    {
        // Arrange
        var opaResponse = new { result = new { allow = true, reason = "Allowed by test" } };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(opaResponse), Encoding.UTF8, "application/json")
        };
        var provider = CreateProviderWithMockHttp(responseMessage);
        var input = new Dictionary<string, object>();

        // Act
        var decision = await provider.EvaluateAsync("test-policy", input, CancellationToken.None);

        // Assert
        decision.IsAllowed.Should().BeTrue();
        decision.Reason.Should().Be("Allowed by test");
    }

    [Fact]
    public async Task EvaluateAsync_OpaDenies_ShouldReturnDeny()
    {
        // Arrange
        var opaResponse = new { result = new { allow = false, reason = "Denied by test" } };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(opaResponse), Encoding.UTF8, "application/json")
        };
        var provider = CreateProviderWithMockHttp(responseMessage);
        var input = new Dictionary<string, object>();

        // Act
        var decision = await provider.EvaluateAsync("test-policy", input, CancellationToken.None);

        // Assert
        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Be("Denied by test");
    }

    [Fact]
    public async Task EvaluateAsync_OpaResponseIsInvalid_ShouldReturnDeny()
    {
        // Arrange
        var opaResponse = new { result = (object?)null };
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(opaResponse), Encoding.UTF8, "application/json")
        };
        var provider = CreateProviderWithMockHttp(responseMessage);
        var input = new Dictionary<string, object>();

        // Act
        var decision = await provider.EvaluateAsync("test-policy", input, CancellationToken.None);

        // Assert
        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Be("Unsupported OPA result format");
    }

    [Fact]
    public async Task EvaluateAsync_OpaRequestFails_ShouldReturnDeny()
    {
        // Arrange
        var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var provider = CreateProviderWithMockHttp(responseMessage);
        var input = new Dictionary<string, object>();

        // Act
        var decision = await provider.EvaluateAsync("test-policy", input, CancellationToken.None);

        // Assert
        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Be("OPA request failed with status code 500");
    }
}

public class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(response);
    }
}
