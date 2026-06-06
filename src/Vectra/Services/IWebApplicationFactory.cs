namespace Vectra.Services;

public interface IWebApplicationFactory
{
    WebApplicationBuilder Create(string[] args);
}