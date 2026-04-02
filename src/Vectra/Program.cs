using Microsoft.AspNetCore.DataProtection;
using Vectra.Extensions;
using Vectra.Infrastructure;
using Vectra.Middleware;

var builder = WebApplication.CreateBuilder(args);

try
{
    ConfigureServices(builder, args);

    var app = builder.Build();
    ConfigureMiddleware(app);
    await ConfigureFlowSynxApplication(app);

    await app.RunAsync();
}
catch (Exception ex)
{
    await HandleStartupExceptionAsync(builder, ex);
}

static void ConfigureServices(WebApplicationBuilder builder, string[] args)
{
    var services = builder.Services;
    var config = builder.Configuration;
    var env = builder.Environment;

    services.AddDataProtection()
            .SetApplicationName("VectraGateway");

    services
        .AddInfrastructure(builder.Configuration)
        .AddOpenApi()
        .AddVectraProxyForwarder();

    if (!env.IsDevelopment())
        builder.Services.ParseVectraArguments(args);

    builder.ConfigureVectraHttpServer();
}

static void ConfigureMiddleware(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseVectraCustomException();
    }

    app.UseVectraHttps();
    app.UseVectraCustomHeaders();

    app.UseRouting();

    app.UseVectraApiDocumentation();
    app.UseVectraHealthCheck();

    app.UseMiddleware<ProxyMiddleware>();
}

static async Task ConfigureFlowSynxApplication(WebApplication app)
{
    await app.EnsureApplicationDatabaseCreated();
    app.MapEndpoints();
}

static async Task HandleStartupExceptionAsync(WebApplicationBuilder builder, Exception ex)
{
    try
    {
        using var scope = builder.Services.BuildServiceProvider().CreateScope();
        var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Unhandled exception during startup");
    }
    catch
    {
        await Console.Error.WriteLineAsync($"Startup error: {ex.Message}");
    }

    // Prevent console from closing immediately
    await Task.Delay(500);
}