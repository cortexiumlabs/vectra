using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text.Json;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;
using Vectra.Infrastructure.HumanInTheLoop.Notifiers;

namespace Vectra.Infrastructure.UnitTests.HumanInTheLoop.Notifiers;

public class GenericWebhookNotifierTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly ILogger<GenericWebhookNotifier> _logger = Substitute.For<ILogger<GenericWebhookNotifier>>();

    private GenericWebhookNotifier CreateSut(GenericWebhookNotificationConfiguration? webhookConfig = null)
    {
        var config = new HumanInTheLoopConfiguration
        {
            Notifications = new NotificationSettings
            {
                GenericWebhook = webhookConfig ?? new GenericWebhookNotificationConfiguration
                {
                    Enabled = true,
                    WebhookUrl = "https://webhook.example.com/hitl",
                    Headers = new Dictionary<string, string>
                    {
                        { "X-Api-Key", "secret-key" }
                    }
                }
            }
        };

        return new GenericWebhookNotifier(Options.Create(config), _httpClientFactory, _logger);
    }

    private HitlNotification CreateNotification() =>
        new(
            Id: "test-abc",
            AgentId: Guid.NewGuid(),
            Method: "PATCH",
            Url: "https://api.example.com/resource/456",
            Reason: "Partial update requires review",
            Timestamp: new DateTime(2024, 1, 15, 16, 0, 0, DateTimeKind.Utc),
            ExpiresAt: new DateTime(2024, 1, 15, 17, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task NotifyAsync_WhenDisabledAndNoLegacyUrl_DoesNotSendRequest()
    {
        var config = new GenericWebhookNotificationConfiguration { Enabled = false, WebhookUrl = null };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_WhenWebhookUrlIsNull_DoesNotSendRequest()
    {
        var config = new GenericWebhookNotificationConfiguration { Enabled = true, WebhookUrl = null };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_UsesLegacyWebhookUrl_WhenNewConfigNotEnabled()
    {
        var config = new GenericWebhookNotificationConfiguration { Enabled = false, WebhookUrl = null };
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.OK, "ok");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut(config);
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.RequestCount.Should().Be(1);
        mockHandler.LastRequest.Should().NotBeNull();
        mockHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        mockHandler.LastRequest.RequestUri!.ToString().Should().Be("https://legacy.webhook.com/hitl");
    }

    [Fact]
    public async Task NotifyAsync_WhenEnabled_SendsWebhookRequest()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.OK, "ok");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.RequestCount.Should().Be(1);
        mockHandler.LastRequest.Should().NotBeNull();
        mockHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        mockHandler.LastRequest.RequestUri!.ToString().Should().Be("https://webhook.example.com/hitl");
    }

    [Fact]
    public async Task NotifyAsync_SendsCorrectJsonPayload()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.OK, "ok");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.LastRequest.Should().NotBeNull();
        var content = await mockHandler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Deserialize<JsonElement>(content);

        // JSON properties use camelCase naming by default
        payload.GetProperty("id").GetString().Should().Be(notification.Id);
        payload.GetProperty("agentId").GetString().Should().Be(notification.AgentId.ToString());
        payload.GetProperty("method").GetString().Should().Be(notification.Method);
        payload.GetProperty("url").GetString().Should().Be(notification.Url);
        payload.GetProperty("reason").GetString().Should().Be(notification.Reason);
    }

    [Fact]
    public async Task NotifyAsync_IncludesCustomHeaders()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.OK, "ok");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var config = new GenericWebhookNotificationConfiguration
        {
            Enabled = true,
            WebhookUrl = "https://webhook.example.com/hitl",
            Headers = new Dictionary<string, string>
            {
                { "X-Api-Key", "secret-key" },
                { "X-Custom-Header", "custom-value" }
            }
        };
        var sut = CreateSut(config);
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.LastRequest.Should().NotBeNull();
        mockHandler.LastRequest!.Headers.GetValues("X-Api-Key").Should().Contain("secret-key");
        mockHandler.LastRequest.Headers.GetValues("X-Custom-Header").Should().Contain("custom-value");
    }

    [Fact]
    public async Task NotifyAsync_WhenRequestFails_LogsWarning()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "Service unavailable");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Generic webhook notification")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task NotifyAsync_WhenHttpClientThrows_LogsError()
    {
        var mockHandler = new FailingHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to send generic webhook notification")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public int RequestCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        public TestHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            });
        }
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new TimeoutException("Request timed out");
    }
}
