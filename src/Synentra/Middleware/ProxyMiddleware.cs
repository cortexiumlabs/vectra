using Microsoft.AspNetCore.WebUtilities;
using Synentra.Application.Abstractions.CircuitBreaker;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.RateLimit;
using Synentra.Application.Abstractions.Security;
using Synentra.Application.Models;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.Infrastructure.Decision;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Synentra.Middleware;

public class ProxyMiddleware
{
    private static readonly Regex MultipleSlashesRegex = new(
        @"/{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(3));

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(3));

    private static readonly Regex NumericIdRegex = new(
        @"^\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(3));

    private static readonly Regex HexIdRegex = new(
        @"^[a-fA-F0-9]{16,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(3));

    private static readonly Regex UlidRegex = new(
        @"^[0-9A-HJKMNP-TV-Z]{26}$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(3));

    private static readonly Regex OpaqueIdRegex = new(
        @"^(?=.*\d)[A-Za-z0-9_]{20,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(3));

    private static readonly Regex PrefixedIdRegex = new(
        @"^(?:id|ord|order|usr|user|acct|account|req|request|job|txn|" +
        @"transaction|inv|invoice|ticket|case|res|resource|tenant|tn|" +
        @"svc|service|proj|project|sub|subscription|cus|customer)" +
        @"[-_][A-Za-z0-9_-]{4,}$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(3));

    private static readonly Regex IsoDateRegex = new(
        @"^\d{4}-\d{2}-\d{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(3));

    private static readonly Regex DurationRegex = new(
        @"^\d+(?:ms|s|m|h|d|w)$",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(3));

    private static readonly HashSet<string> PreservedQueryValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
        "true",
        "false",
        "asc",
        "desc",
        "active",
        "inactive",
        "pending",
        "approved",
        "rejected",
        "failed",
        "completed",
        "open",
        "closed",
        "all",
        "none"
        };

    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;

    public ProxyMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Extract raw target URL from the path
        var fullPath = context.Request.Path.ToString();
        if (!fullPath.StartsWith("/proxy/"))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid proxy path. Expected /proxy/<full-url>");
            return;
        }

        var targetUrlString = fullPath.Substring("/proxy/".Length);
        if (!Uri.TryCreate(targetUrlString, UriKind.Absolute, out var targetUri))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid target URL in proxy path");
            return;
        }

        // 3. Override request path and query for YARP
        context.Request.Path = targetUri.AbsolutePath;
        context.Request.QueryString = new QueryString(targetUri.Query);

        // 4. Resolve services
        var decisionEngine = context.RequestServices.GetRequiredService<IDecisionEngine>();
        var hitlService = context.RequestServices.GetRequiredService<IHitlService>();
        var accessService = context.RequestServices.GetRequiredService<IAgentRequestAccessService>();

        Guid agentId;
        double trustScore;

        // 5. JWT – require valid agent identity
        if (!context.Items.TryGetValue("AgentId", out var agentIdObj) || agentIdObj is not Guid authenticatedId)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Missing or invalid authentication");
            return;
        }

        agentId = authenticatedId;

        var access = await accessService.GetAgentAsync(agentId, context.RequestAborted);
        if (!access.IsAllowed || access.Agent is null)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync(access.ForbiddenReason ?? "Agent is not active");
            return;
        }

        var agent = access.Agent;
        trustScore = agent.TrustScore;

        // 6. Rate limiting – 429 if agent exceeded requests/min
        var rateLimiter = context.RequestServices.GetRequiredService<IAgentRateLimiter>();
        if (!await rateLimiter.IsAllowedAsync(agentId, context.RequestAborted))
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsync("Rate limit exceeded. Try again in 60 seconds.");
            return;
        }

        // 7. Circuit breaker – 503 if upstream is currently open
        var circuitBreaker = context.RequestServices.GetRequiredService<ICircuitBreaker>();
        var upstreamHost = targetUri.Host;
        if (!circuitBreaker.IsAllowed(upstreamHost))
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync($"Upstream '{upstreamHost}' is temporarily unavailable.");
            return;
        }
        context.Request.EnableBuffering();
        var requestContext = new RequestContext
        {
            Method = context.Request.Method,
            Path = targetUri.PathAndQuery,
            TargetUrl = targetUri.ToString(),
            Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            AgentId = agentId,
            PolicyName = agent.PolicyName,
            TrustScore = trustScore,
            Body = await ReadBodyAsync(context.Request)
        };

        context.Items["PolicyName"] = requestContext.PolicyName;
        context.Items["PolicyVersion"] = requestContext.PolicyName;
        context.Items["TargetUrl"] = requestContext.TargetUrl;

        var semanticInput = BuildSemanticInput(requestContext);

        var decision = await decisionEngine.EvaluateAsync(semanticInput, requestContext, context.RequestAborted);

        context.Items["RiskScore"] = decision.TrustScore;
        context.Items["Decision"] = decision.Type.ToString().ToLowerInvariant();
        context.Items["DecisionReason"] = decision.Reason;

        if (decision.IsDenied)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync(decision.Reason ?? "Access denied");
            return;
        }

        if (decision.IsHitl)
        {
            var hitlId = await hitlService.SuspendRequestAsync(requestContext, decision.Reason ?? "HITL required");
            context.Response.StatusCode = 202;
            context.Response.Headers.Location = $"/hitls/{hitlId}";
            await context.Response.WriteAsync($"Request pending approval. Poll {context.Response.Headers.Location}");
            return;
        }

        // 8. Forward the request with CORRECT headers
        context.Request.Body.Position = 0;

        var securityOptions = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecurityConfiguration>>();
        var synentraAuthHeaderName = !string.IsNullOrWhiteSpace(securityOptions.Value.AgentAuth.CustomHeaderName)
            ? securityOptions.Value.AgentAuth.CustomHeaderName
            : "Synentra-Authorization";

        // Create a new HttpRequestMessage for manual forwarding (or use YARP with transforms)
        var httpClient = _httpClientFactory.CreateClient();
        var proxyRequest = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = targetUri,
            Content = context.Request.Body.Length > 0 ? new StreamContent(context.Request.Body) : null
        };

        // Copy headers, but exclude Aegis-specific ones
        foreach (var header in context.Request.Headers)
        {
            // Skip headers that must NOT be forwarded
            if (header.Key.Equals(synentraAuthHeaderName, StringComparison.OrdinalIgnoreCase) ||
                header.Key == "Host" ||                   // Will be set from RequestUri
                header.Key == "Connection" ||
                header.Key == "Content-Length")           // Handled by HttpClient
                continue;

            // Keep all other headers (including Accept, AgentId, etc.)
            proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToString());
        }

        // Optionally inject a real API key (if needed for the target)
        // proxyRequest.Headers.Add("X-API-Key", "your-secret-key");

        // Send the request
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead);
            circuitBreaker.RecordSuccess(upstreamHost);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            circuitBreaker.RecordFailure(upstreamHost);
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync($"Upstream '{upstreamHost}' is unavailable.");
            return;
        }

        // Copy response back
        context.Response.StatusCode = (int)response.StatusCode;
        if ((int)response.StatusCode >= 500)
            circuitBreaker.RecordFailure(upstreamHost);

        // Headers that must not be copied — ASP.NET Core / HttpClient manage these
        static bool IsRestrictedHeader(string name) =>
            name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Content-Length",    StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Proxy-Connection",  StringComparison.OrdinalIgnoreCase) ||
            name.Equals("TE",                StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Connection",        StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Keep-Alive",        StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Upgrade",           StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Trailer",           StringComparison.OrdinalIgnoreCase);

        foreach (var header in response.Headers)
        {
            if (!IsRestrictedHeader(header.Key))
                context.Response.Headers[header.Key] = string.Join(", ", header.Value);
        }
        foreach (var header in response.Content.Headers)
        {
            if (!IsRestrictedHeader(header.Key))
                context.Response.Headers[header.Key] = string.Join(", ", header.Value);
        }

        await response.Content.CopyToAsync(context.Response.Body);
    }

    private async Task<string?> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0) return null;

        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        var isJson = request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true
                  || request.ContentType?.Contains("+json", StringComparison.OrdinalIgnoreCase) == true;

        if (!isJson) return body; // keep raw text for semantic/risk analysis

        try
        {
            return JsonToIntentText.Convert(body);
        }
        catch (JsonException)
        {
            return body; // resilient fallback
        }
    }

    private string BuildSemanticInput(RequestContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var method = string.IsNullOrWhiteSpace(ctx.Method)
            ? "NONE"
            : ctx.Method.Trim().ToUpperInvariant();

        var path = NormalizePath(ctx.Path);

        var contentType = ctx.Headers.TryGetValue("Content-Type", out var ct)
            ? NormalizeContentType(ct.ToString())
            : "none";

        var body = string.IsNullOrWhiteSpace(ctx.Body)
            ? "none"
            : NormalizeWhitespace(ctx.Body);

        return
            $"method: {method} [SEP] " +
            $"path: {path} [SEP] " +
            $"body: {body} [SEP] " +
            $"content_type: {contentType}";
    }

    private static string NormalizePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "/";

        var value = input.Trim();

        // If an absolute URL is accidentally provided, discard the hostname.
        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            value = absoluteUri.PathAndQuery;

        // Fragments are not sent to HTTP servers and should not affect intent.
        var fragmentIndex = value.IndexOf('#');
        if (fragmentIndex >= 0)
            value = value[..fragmentIndex];

        var queryIndex = value.IndexOf('?');

        var rawPath = queryIndex >= 0
            ? value[..queryIndex]
            : value;

        var rawQuery = queryIndex >= 0
            ? value[(queryIndex + 1)..]
            : string.Empty;

        rawPath = rawPath.Replace('\\', '/');
        rawPath = MultipleSlashesRegex.Replace(rawPath, "/");

        if (!rawPath.StartsWith('/'))
            rawPath = "/" + rawPath;

        var normalizedSegments = rawPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizePathSegment);

        var normalizedPath = "/" + string.Join("/", normalizedSegments);

        // Remove the trailing slash, except for the root path.
        if (normalizedPath.Length > 1)
            normalizedPath = normalizedPath.TrimEnd('/');

        var normalizedQuery = NormalizeQueryString(rawQuery);

        return normalizedQuery.Length == 0
            ? normalizedPath
            : $"{normalizedPath}?{normalizedQuery}";
    }

    private static string NormalizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return string.Empty;

        string decoded;

        try
        {
            decoded = Uri.UnescapeDataString(segment);
        }
        catch (UriFormatException)
        {
            decoded = segment;
        }

        decoded = decoded.Trim();

        if (decoded.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            decoded.Equals("{id}", StringComparison.OrdinalIgnoreCase) ||
            decoded.Equals(":id", StringComparison.OrdinalIgnoreCase))
        {
            return "id";
        }

        if (IsDynamicIdentifier(decoded))
            return "id";

        if (IsoDateRegex.IsMatch(decoded))
            return "date";

        var normalized = NormalizeWhitespace(decoded)
            .Replace(' ', '-')
            .ToLowerInvariant();

        return Uri.EscapeDataString(normalized);
    }

    private static bool IsDynamicIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (Guid.TryParse(value, out _))
            return true;

        if (NumericIdRegex.IsMatch(value))
            return true;

        if (HexIdRegex.IsMatch(value))
            return true;

        if (UlidRegex.IsMatch(value))
            return true;

        if (OpaqueIdRegex.IsMatch(value))
            return true;

        if (PrefixedIdRegex.IsMatch(value))
            return true;

        return false;
    }

    private static string NormalizeQueryString(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var parsedQuery = QueryHelpers.ParseQuery("?" + query);
        var normalizedParameters = new List<(string Key, string Value)>();

        foreach (var parameter in parsedQuery)
        {
            var key = NormalizeQueryKey(parameter.Key);

            foreach (var value in parameter.Value)
            {
                normalizedParameters.Add(
                    (key, NormalizeQueryValue(key, value)));
            }

            // Preserve query keys with no assigned value.
            if (parameter.Value.Count == 0)
                normalizedParameters.Add((key, "none"));
        }

        return string.Join(
            "&",
            normalizedParameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .ThenBy(parameter => parameter.Value, StringComparer.Ordinal)
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}=" +
                    $"{Uri.EscapeDataString(parameter.Value)}"));
    }

    private static string NormalizeQueryKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "parameter";

        return NormalizeWhitespace(key)
            .Replace(' ', '_')
            .ToLowerInvariant();
    }

    private static string NormalizeQueryValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "none";

        var normalized = NormalizeWhitespace(value);

        if (IsIdentifierQueryKey(key) || IsDynamicIdentifier(normalized))
            return "id";

        if (IsoDateRegex.IsMatch(normalized) ||
            DateTimeOffset.TryParse(normalized, out _))
        {
            return "date";
        }

        if (DurationRegex.IsMatch(normalized))
            return "duration";

        if (decimal.TryParse(
                normalized,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out _))
        {
            return "number";
        }

        if (PreservedQueryValues.Contains(normalized))
            return normalized.ToLowerInvariant();

        // Avoid allowing customer names, tokens, search terms, or arbitrary
        // high-cardinality values to become part of the classifier vocabulary.
        return "value";
    }

    private static bool IsIdentifierQueryKey(string key)
    {
        return key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("-id", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("cursor", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("token", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("request_id", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("correlation_id", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeContentType(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "none";

        // Content-Type should contain one value. Taking the first also handles
        // improperly combined header values defensively.
        var firstValue = input
            .Split(',', 2, StringSplitOptions.TrimEntries)[0];

        if (!MediaTypeHeaderValue.TryParse(firstValue, out var parsed) ||
            string.IsNullOrWhiteSpace(parsed.MediaType))
        {
            return "other";
        }

        var mediaType = parsed.MediaType.ToLowerInvariant();

        // Normalize nonstandard JSON aliases.
        if (mediaType is "text/json" or "application/x-json")
            return "application/json";

        // Preserve JSON media types that carry useful operation semantics.
        if (mediaType is
            "application/json-patch+json" or
            "application/merge-patch+json" or
            "application/problem+json")
        {
            return mediaType;
        }

        // Normalize vendor-specific JSON and XML types.
        if (mediaType.EndsWith("+json", StringComparison.Ordinal))
            return "application/json";

        if (mediaType.EndsWith("+xml", StringComparison.Ordinal))
            return "application/xml";

        // Parameters such as charset and multipart boundary are intentionally
        // removed by returning only MediaType.
        return mediaType;
    }

    private static string NormalizeWhitespace(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalizedUnicode = input.Normalize(NormalizationForm.FormC);

        return WhitespaceRegex
            .Replace(normalizedUnicode, " ")
            .Trim();
    }
}