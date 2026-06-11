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

public class SlackNotifierTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly ILogger<SlackNotifier> _logger = Substitute.For<ILogger<SlackNotifier>>();

    private SlackNotifier CreateSut(SlackNotificationConfiguration? slackConfig = null)
    {
        var config = new HumanInTheLoopConfiguration
        {
            Notifications = new NotificationSettings
            {
                Slack = slackConfig ?? new SlackNotificationConfiguration
                {
                    Enabled = true,
                    WebhookUrl = "https://hooks.slack.com/services/TEST",
                    Username = "Synentra Bot",
                    IconEmoji = ":robot:"
                }
            }
        };

        return new SlackNotifier(Options.Create(config), _httpClientFactory, _logger);
    }

    private HitlNotification CreateNotification() =>
        new(
            Id: "test-123",
            AgentId: Guid.NewGuid(),
            Method: "POST",
            Url: "https://api.example.com/data",
            Reason: "High risk operation",
            Timestamp: new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            ExpiresAt: new DateTime(2024, 1, 15, 11, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task NotifyAsync_WhenDisabled_DoesNotSendRequest()
    {
        var config = new SlackNotificationConfiguration { Enabled = false };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_WhenWebhookUrlIsNull_DoesNotSendRequest()
    {
        var config = new SlackNotificationConfiguration { Enabled = true, WebhookUrl = null };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_WhenEnabled_SendsSlackMessage()
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
        mockHandler.LastRequest.RequestUri!.ToString().Should().Be("https://hooks.slack.com/services/TEST");
    }

    [Fact]
    public async Task NotifyAsync_SendsCorrectPayloadFormat()
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

        payload.GetProperty("text").GetString().Should().Contain("HITL Review Required");
        payload.GetProperty("text").GetString().Should().Contain(notification.Id);
        payload.GetProperty("username").GetString().Should().Be("Synentra Bot");
        payload.GetProperty("icon_emoji").GetString().Should().Be(":robot:");
    }

    [Fact]
    public async Task NotifyAsync_WhenRequestFails_LogsWarning()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.InternalServerError, "Server error");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        // Verify a warning was logged (NSubstitute syntax for ILogger)
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Slack notification")),
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

        // Verify an error was logged
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to send Slack notification")),
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
            => throw new HttpRequestException("Network error");
    }
}
