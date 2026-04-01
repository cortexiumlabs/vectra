//using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Logging;
//using Vectra.Core.DTOs;
//using Vectra.Core.Interfaces;
//using Vectra.Core.UseCases;
//using Yarp.ReverseProxy.Forwarder;

//namespace Vectra.Infrastructure.Proxy;

//public class ProxyMiddleware
//{
//    private readonly RequestDelegate _next;
//    private readonly IHttpForwarder _forwarder;
//    private readonly ILogger<ProxyMiddleware> _logger;
//    private readonly EvaluateRequestUseCase _evaluateUseCase;
//    private readonly IHitlService _hitlService;
//    private readonly IHttpClientFactory _httpClientFactory;

//    public ProxyMiddleware(
//        RequestDelegate next,
//        IHttpForwarder forwarder,
//        ILogger<ProxyMiddleware> logger,
//        EvaluateRequestUseCase evaluateUseCase,
//        IHitlService hitlService,
//        IHttpClientFactory httpClientFactory)
//    {
//        _next = next;
//        _forwarder = forwarder;
//        _logger = logger;
//        _evaluateUseCase = evaluateUseCase;
//        _hitlService = hitlService;
//        _httpClientFactory = httpClientFactory;
//    }

//    public async Task InvokeAsync(HttpContext context)
//    {
//        // 1. Extract JWT and build RequestContext
//        var agentId = GetAgentIdFromToken(context);
//        if (agentId == null)
//        {
//            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
//            await context.Response.WriteAsync("Missing or invalid token");
//            return;
//        }

//        var requestContext = new RequestContext
//        {
//            Method = context.Request.Method,
//            Path = context.Request.Path + context.Request.QueryString,
//            Headers = context.Request.Headers.ToDictionary(
//                h => h.Key,
//                h => h.Value.ToString()
//            ),
//            AgentId = agentId.Value,
//            TrustScore = 0.5, // retrieve from DB or cache
//        };

//        // 2. Evaluate policy
//        var decision = await _evaluateUseCase.ExecuteAsync(requestContext);

//        if (decision.IsDenied)
//        {
//            context.Response.StatusCode = StatusCodes.Status403Forbidden;
//            await context.Response.WriteAsync(decision.Reason ?? "Access denied");
//            return;
//        }

//        if (decision.IsHitl)
//        {
//            var hitlId = await _hitlService.SuspendRequestAsync(requestContext, decision.Reason ?? "HITL required");
//            context.Response.StatusCode = StatusCodes.Status202Accepted;
//            context.Response.Headers.Location = $"/hitl/status/{hitlId}";
//            await context.Response.WriteAsync($"Request pending approval. Poll {context.Response.Headers.Location}");
//            return;
//        }

//        // 3. Allowed – forward using YARP
//        // We need to determine the destination (could be based on host header or config)
//        // For simplicity, we'll forward to a default upstream from config.
//        var destination = context.Request.Host.Value; // or lookup based on original host
//        // Here we could inject real API keys from Vault into headers.
//        // For example: context.Request.Headers["X-API-Key"] = "real-key";

//        var forwarderRequest = context.Request;
//        var forwarderResponse = context.Response;
//        var forwarderContext = new ForwarderRequestContext
//        {
//            Activity = context.Items["Activity"] as System.Diagnostics.Activity,
//            HttpContext = context
//        };
//        await _forwarder.SendAsync(forwarderContext, destination, _httpClientFactory.CreateClient());
//    }

//    private Guid? GetAgentIdFromToken(HttpContext context)
//    {
//        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
//        if (string.IsNullOrEmpty(token)) return null;

//        // We'll use a token validation service (ITokenService) – but we need to inject it.
//        // For simplicity, we'll assume we have a service. In real code, inject ITokenService.
//        // var principal = _tokenService.ValidateToken(token);
//        // if (principal == null) return null;
//        // var idString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//        // return Guid.TryParse(idString, out var id) ? id : null;

//        // Placeholder:
//        return Guid.NewGuid(); // replace with actual validation
//    }
//}