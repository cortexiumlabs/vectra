namespace Vectra.Services;

public sealed class DefaultWebApplicationFactory : IWebApplicationFactory
{
    public WebApplicationBuilder Create(string[] args)
        => WebApplication.CreateBuilder(args);
}