using Microsoft.AspNetCore.DataProtection;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Synentra.Extensions;
using Synentra.Infrastructure;
using Synentra.Middleware;
using Synentra.Application;

namespace Synentra.Services;

[ExcludeFromCodeCoverage]
internal sealed class StartupConfiguration : IStartupConfiguration
{
    public void ConfigureServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;
        var environment = builder.Environment;

        // Data Protection
        services.AddDataProtection()
                .SetApplicationName("SynentraGateway");

        // JSON options
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // Synentra-specific service registrations
        services
            .AddSynentraConfiguration(configuration)
            .AddSystemClock()
            .AddJsonSerialization()
            .AddCache()
            .AddInfrastructure()
            .AddSynentraPersistence()
            .AddSynentraApiDocumentation()
            .AddSynentraProxyForwarder()
            .AddSynentraHealthChecker()
            .AddSynentraVersion()
            .AddSynentraApplication();

        builder.AddSynentraObservability();

        // HTTP server configuration (Kestrel, etc.)
        builder.ConfigureSynentraHttpServer();
    }

    public async Task ConfigurePipelineAsync(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseSynentraCustomException();
        }

        app.UseSynentraHttps();
        app.UseSynentraCustomHeaders();

        app.UseRouting();
        app.UseMiddleware<AgentAuthMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();

        // Proxy branch
        app.MapWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/proxy"),
            proxyBranch => proxyBranch.UseMiddleware<ProxyMiddleware>());

        app.MapEndpoints();

        // Catch-all 404 handler
        app.Map("/{**catch-all}", async context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"No endpoint found for {context.Request.Path}");
        });

        app.UseSynentraApiDocumentation();
        app.UseSynentraHealthCheck();

        // Ensure database is created/migrated
        await app.EnsureApplicationDatabaseCreated();
    }
}