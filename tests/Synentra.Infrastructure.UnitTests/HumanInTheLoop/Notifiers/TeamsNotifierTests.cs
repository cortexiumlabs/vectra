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

public class TeamsNotifierTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly ILogger<TeamsNotifier> _logger = Substitute.For<ILogger<TeamsNotifier>>();

    private TeamsNotifier CreateSut(TeamsNotificationConfiguration? teamsConfig = null)
    {
        var config = new HumanInTheLoopConfiguration
        {
            Notifications = new NotificationSettings
            {
                Teams = teamsConfig ?? new TeamsNotificationConfiguration
                {
                    Enabled = true,
                    WebhookUrl = "https://outlook.office.com/webhook/TEST",
                    ThemeColor = "FF0000"
                }
            }
        };

        return new TeamsNotifier(Options.Create(config), _httpClientFactory, _logger);
    }

    private HitlNotification CreateNotification() =>
        new(
            Id: "test-456",
            AgentId: Guid.NewGuid(),
            Method: "DELETE",
            Url: "https://api.example.com/users/123",
            Reason: "Destructive operation",
            Timestamp: new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            ExpiresAt: new DateTime(2024, 1, 15, 15, 30, 0, DateTimeKind.Utc));

    [Fact]
    public async Task NotifyAsync_WhenDisabled_DoesNotSendRequest()
    {
        var config = new TeamsNotificationConfiguration { Enabled = false };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_WhenWebhookUrlIsNull_DoesNotSendRequest()
    {
        var config = new TeamsNotificationConfiguration { Enabled = true, WebhookUrl = null };
        var sut = CreateSut(config);

        await sut.NotifyAsync(CreateNotification(), CancellationToken.None);

        _httpClientFactory.DidNotReceive().CreateClient();
    }

    [Fact]
    public async Task NotifyAsync_WhenEnabled_SendsTeamsMessage()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.OK, "1");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.RequestCount.Should().Be(1);
        mockHandler.LastRequest.Should().NotBeNull();
        mockHandler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        mockHandler.LastRequest.RequestUri!.ToString().Should().Be("https://outlook.office.com/webhook/TEST");
    }

    [Fact]
    public async Task NotifyAsync_SendsCorrectMessageCardFormat()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.OK, "1");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        mockHandler.LastRequest.Should().NotBeNull();
        var content = await mockHandler.LastRequest!.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Deserialize<JsonElement>(content);

        payload.GetProperty("@type").GetString().Should().Be("MessageCard");
        payload.GetProperty("@context").GetString().Should().Be("https://schema.org/extensions");
        payload.GetProperty("themeColor").GetString().Should().Be("FF0000");
        payload.GetProperty("summary").GetString().Should().Be("HITL Review Required");

        var sections = payload.GetProperty("sections");
        sections.GetArrayLength().Should().BeGreaterThan(0);

        var firstSection = sections[0];
        firstSection.GetProperty("activityTitle").GetString().Should().Contain("HITL Review Required");

        var facts = firstSection.GetProperty("facts");
        facts.GetArrayLength().Should().Be(6); // Agent ID, Method, URL, Reason, Expires In, Timestamp
    }

    [Fact]
    public async Task NotifyAsync_WhenRequestFails_LogsWarning()
    {
        var mockHandler = new TestHttpMessageHandler(HttpStatusCode.BadRequest, "Invalid payload");
        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient().Returns(httpClient);

        var sut = CreateSut();
        var notification = CreateNotification();

        await sut.NotifyAsync(notification, CancellationToken.None);

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Teams notification")),
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
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to send Teams notification")),
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
            => throw new TaskCanceledException("Request timeout");
    }
}
