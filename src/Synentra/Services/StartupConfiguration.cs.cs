using Microsoft.AspNetCore.DataProtection;
using Synentra.Application;
using Synentra.Extensions;
using Synentra.Infrastructure;
using Synentra.Middleware;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

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
        var dpKeysDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Synentra",
            "DataProtection",
            "Keys");

        Directory.CreateDirectory(dpKeysDir);

        var dataProtectionBuilder = services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeysDir))
            .SetApplicationName("Synentra");

        // Apply DPAPI encryption only on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            dataProtectionBuilder.ProtectKeysWithDpapi();

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

        services.AddCors(options =>
        {
            options.AddPolicy("ConsoleCors", policy =>
            {
                policy
                    .WithOrigins("https://localhost:7181")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Content-Type", "Authorization", "Synentra-Authorization");
            });
        });

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

        app.UseCors("ConsoleCors");

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