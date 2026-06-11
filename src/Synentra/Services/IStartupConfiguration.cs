namespace Vectra.Services;

public interface IStartupConfiguration
{
    void ConfigureServices(WebApplicationBuilder builder);
    Task ConfigurePipelineAsync(WebApplication app);
}