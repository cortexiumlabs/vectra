using System.Diagnostics.CodeAnalysis;

namespace Synentra.Services;

[ExcludeFromCodeCoverage]
public sealed class DefaultWebApplicationFactory : IWebApplicationFactory
{
    public WebApplicationBuilder Create(string[] args)
        => WebApplication.CreateBuilder(args);
}