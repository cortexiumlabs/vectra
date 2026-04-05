using Vectra.Application.Abstractions.Versioning;

namespace Vectra.Middleware;

public sealed class VersionHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IVersion _version;

    public VersionHeaderMiddleware(RequestDelegate next, IVersion version)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _version = version ?? throw new ArgumentNullException(nameof(version));
    }

    public Task Invoke(HttpContext context)
    {
        context.Response.Headers["vectra-version"] = _version.Version.ToString();
        return _next(context);
    }
}