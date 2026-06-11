namespace Synentra.Services;

public interface IWebApplicationFactory
{
    WebApplicationBuilder Create(string[] args);
}