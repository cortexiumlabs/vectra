using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text.Json;
using Synentra.Application.Abstractions.Executions;
using Synentra.BuildingBlocks.Configuration.HumanInTheLoop;
using Synentra.Infrastructure.HumanInTheLoop.Notifiers;

namespace Synentra.Infrastructure.UnitTests.HumanInTheLoop.Notifiers;

public class PagerDutyNotifierTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly ILogger<PagerDutyNotifier> _logger = Substitute.For<ILogger<PagerDutyNotifier>>();

    private PagerDutyNotifier CreateSut(PagerDutyNotificationConfiguration? pdConfig = null)
    {
        var config = new HumanInTheLoopConfiguration
        {
            Notifications = new NotificationSettings
            {
                PagerDuty = pdConfig ?? new PagerDutyNotificationConfiguration
                {
                    Enabled = true,
                    RoutingKey = "test-routing-key-123",
                    ApiUrl = "https://events.pagerduty.com/v2/enqueue",
                    Severity = "critical"
                }
            }
        };

        return new PagerDutyNotifier(Options.Create(config), _httpClientFactory, _logger);
    }

    private HitlNotification CreateNotification() =>
        new(
            Id: "test-789",
            AgentId: Guid.NewGuid(),
            Method: "PUT",
            Url: "https://api.example.com/config",
            Reason: "Critical configuration change",
            Timestamp: new DateTime(2024, 1, 15, 9, 15, 0, DateTimeKind.Utc),
            ExpiresAt: new DateTime(2024, 1, 15, 9, 45, 0, DateTimeKind.Utc));

    [Fact]
    public async Task NotifyAsync_WhenDisabled_DoesNotSendRequest()
    {
        var config = new PagerDutyNotificationConfiguration { Enabled = false };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_WhenRoutingKeyIsNull_DoesNotSendRequest()
    {
        var config = new PagerDutyNotificationConfiguration { Enabled = true, RoutingKey = null };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_WhenEnabled_SendsPagerDutyEvent()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.Accepted, "{\"status\":\"success\"}");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.RequestCount.Should().Be(1);
        mockHandler.LastRequest.Should().NotBeNull();
        mockHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        mockHandler.LastRequest.RequestUri!.ToString().Should().Be("https://events.pagerduty.com/v2/enqueue");
    }

    [Fact]
    public async Task NotifyAsync_SendsCorrectEventsApiV2Format()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.Accepted, "{\"status\":\"success\"}");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.LastRequest.Should().NotBeNull();
        var content = await mockHandler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Deserialize<JsonElement>(content);

        payload.GetProperty("routing_key").GetString().Should().Be("test-routing-key-123");
        payload.GetProperty("event_action").GetString().Should().Be("trigger");
        payload.GetProperty("dedup_key").GetString().Should().Be($"hitl-{notification.Id}");

        var payloadProperty = payload.GetProperty("payload");
        payloadProperty.GetProperty("summary").GetString().Should().Contain(notification.Method);
        payloadProperty.GetProperty("source").GetString().Should().Be("Synentra");
        payloadProperty.GetProperty("severity").GetString().Should().Be("critical");
        payloadProperty.GetProperty("component").GetString().Should().Be("HITL");

        var customDetails = payloadProperty.GetProperty("custom_details");
        customDetails.GetProperty("request_id").GetString().Should().Be(notification.Id);
        customDetails.GetProperty("method").GetString().Should().Be(notification.Method);
        customDetails.GetProperty("url").GetString().Should().Be(notification.Url);
        customDetails.GetProperty("reason").GetString().Should().Be(notification.Reason);
    }

    [Fact]
    public async Task NotifyAsync_UsesDedupKeyForEventDeduplication()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.Accepted, "{\"status\":\"success\"}");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.LastRequest.Should().NotBeNull();
        var content = await mockHandler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Deserialize<JsonElement>(content);

        // Verify dedup_key format
        payload.GetProperty("dedup_key").GetString().Should().StartWith("hitl-");
        payload.GetProperty("dedup_key").GetString().Should().Contain(notification.Id);
    }

    [Fact]
    public async Task NotifyAsync_WhenRequestFails_LogsWarning()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.BadRequest, "Invalid routing key");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("PagerDuty notification")),
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
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to send PagerDuty notification")),
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
            => throw new HttpRequestException("DNS resolution failed");
    }
}
