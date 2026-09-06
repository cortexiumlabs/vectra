using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using Synentra.Application.Abstractions.Serializations;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.HealthCheck;
using Synentra.Infrastructure.Persistence.Common;
using Synentra.Middleware;

namespace Synentra.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSynentraCustomException(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseSynentraCustomHeaders(this IApplicationBuilder app)
    {
        // Inject IVersion via middleware instead of locating from ApplicationServices
        app.UseMiddleware<VersionHeaderMiddleware>();
        return app;
    }

    [ExcludeFromCodeCoverage]
    public static IApplicationBuilder UseSynentraHealthCheck(this IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                },
                ResponseWriter = async (context, report) =>
                {
                    // Resolve per-request instead of using ApplicationServices at startup
                    var serializer = context.RequestServices.GetRequiredService<ISerializer>();

                    context.Response.ContentType = "application/json";
                    var response = new HealthCheckResponse
                    {
                        Status = report.Status.ToString(),
                        HealthCheckDuration = report.TotalDuration
                    };
                    await context.Response.WriteAsync(serializer.Serialize(response));
                }
            });
        });

        return app;
    }

    [ExcludeFromCodeCoverage]
    public static IApplicationBuilder UseSynentraApiDocumentation(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        app.UseSwagger(options =>
        {
            options.RouteTemplate = $"open-api/{{documentName}}/specifications.json";
        });

        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "open-api";
            options.SwaggerEndpoint($"synentra/specifications.json", $"Synentra API");
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapSwagger();
        });

        return app;
    }

    public static async Task<IApplicationBuilder> EnsureApplicationDatabaseCreated(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        var initializers = serviceScope.ServiceProvider.GetServices<IDatabaseInitializer>();

        foreach (var initializer in initializers)
        {
            await initializer.EnsureDatabaseCreatedAsync();
        }

        return app;
    }

    public static IApplicationBuilder UseSynentraHttps(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var serverConfiguration = scope.ServiceProvider.GetRequiredService<IOptions<SystemConfiguration>>();
        if (serverConfiguration.Value.Server.Https?.Enabled == true)
            app.UseHttpsRedirection();
        return app;
    }

    public static IApplicationBuilder UseSynentraCors(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var serverConfiguration = scope.ServiceProvider.GetRequiredService<IOptions<SystemConfiguration>>();
        if (serverConfiguration.Value.Cors.Enabled == true)
            app.UseCors("SynentraCors");
        return app;
    }
}