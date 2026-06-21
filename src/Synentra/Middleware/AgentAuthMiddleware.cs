using System.Security.Claims;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Security;
using Synentra.BuildingBlocks.Configuration.Security;

namespace Synentra.Middleware;

public class AgentAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AgentAuthMiddleware> _logger;

    public AgentAuthMiddleware(RequestDelegate next, ILogger<AgentAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authenticator = context.RequestServices.GetRequiredService<IAgentAuthenticator>();
        var securityOptions = context.RequestServices.GetRequiredService<IOptions<SecurityConfiguration>>();
        await AttachFromHeaderAsync(context, authenticator, securityOptions.Value);
        await _next(context);
    }

    private async Task AttachFromHeaderAsync(
        HttpContext context,
        IAgentAuthenticator authenticator,
        SecurityConfiguration securityConfiguration)
    {
        var agentAuthOptions = securityConfiguration.AgentAuth;
        if (agentAuthOptions == null)
        {
            // No agent auth config at all – skip entirely
            return;
        }

        var customHeaderName = !string.IsNullOrWhiteSpace(agentAuthOptions.CustomHeaderName)
            ? agentAuthOptions.CustomHeaderName
            : "Synentra-Authorization";

        var hasCustomHeader = context.Request.Headers.TryGetValue(customHeaderName, out _);

        string? credential = null;
        if (agentAuthOptions.UseCustomHeader)
            credential = ExtractBearerToken(context, customHeaderName);

        if (string.IsNullOrWhiteSpace(credential) && !hasCustomHeader && agentAuthOptions.FallbackToAuthorization)
            credential = ExtractBearerToken(context, "Authorization");

        if (string.IsNullOrWhiteSpace(credential))
            return;

        var principal = await authenticator.ValidateAsync(credential, CancellationToken.None);
        if (principal is null)
            return;

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue("sub");

        if (Guid.TryParse(sub, out var agentId))
        {
            context.Items["AgentId"] = agentId;

            var trustClaim = principal.FindFirstValue("trust_score");
            if (double.TryParse(trustClaim, out var trust))
                context.Items["TrustScore"] = trust;
        }
    }

    private static string? ExtractBearerToken(HttpContext context, string headerName)
    {
        var headerValue = context.Request.Headers[headerName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue))
            return null;

        var parts = headerValue.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            return null;

        return parts[1];
    }
}