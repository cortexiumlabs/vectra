using System.Diagnostics;
using Synentra.Application.Abstractions.Versioning;

namespace Synentra.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IVersion _version;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IVersion version)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        var requestId = context.TraceIdentifier;
        context.Response.Headers["X-Request-Id"] = requestId;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var agentId = context.Items.TryGetValue("AgentId", out var agentValue) ? agentValue : null;
            var riskScore = context.Items.TryGetValue("RiskScore", out var riskValue) ? riskValue : null;
            var decision = context.Items.TryGetValue("Decision", out var decisionValue) ? decisionValue : null;
            var policyVersion = context.Items.TryGetValue("PolicyVersion", out var policyValue) ? policyValue : null;
            var decisionReason = context.Items.TryGetValue("DecisionReason", out var reasonValue) ? reasonValue : null;
            var targetUrl = context.Items.TryGetValue("TargetUrl", out var targetValue) ? targetValue : null;
            var errorType = context.Items.TryGetValue("ErrorType", out var errorValue) ? errorValue : null;

            _logger.LogInformation(
                "Request finished {@RequestLog}",
                new
                {
                    request_id = requestId,
                    agent_id = agentId,
                    risk_score = riskScore,
                    decision,
                    latency_ms = stopwatch.ElapsedMilliseconds,
                    policy_version = policyVersion,
                    protocol = context.Request.Protocol,
                    method = context.Request.Method,
                    path = context.Request.Path.Value,
                    status_code = context.Response.StatusCode,
                    user_agent = context.Request.Headers.UserAgent.ToString(),
                    client_ip = context.Connection.RemoteIpAddress?.ToString(),
                    trace_id = context.TraceIdentifier,
                    version = _version.Version.ToString(),
                    decision_reason = decisionReason,
                    target_url = targetUrl,
                    error_type = errorType
                });
        }
    }
}