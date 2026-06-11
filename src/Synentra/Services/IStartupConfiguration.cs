namespace Synentra.Services;

public interface IStartupConfiguration
{
    void ConfigureServices(WebApplicationBuilder builder);
    Task ConfigurePipelineAsync(WebApplication app);
}