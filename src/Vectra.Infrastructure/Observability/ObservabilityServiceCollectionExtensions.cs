using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Vectra.BuildingBlocks.Configuration.Observability;

namespace Vectra.Infrastructure.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var observabilityConfiguration = builder.Configuration
            .GetSection("Observability")
            .Get<ObservabilityConfiguration>() ?? new ObservabilityConfiguration();

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(builder.Environment.ApplicationName);

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(builder.Environment.ApplicationName);

                if (observabilityConfiguration.OpenTelemetry?.Enabled == true && 
                    !string.IsNullOrWhiteSpace(observabilityConfiguration.OpenTelemetry.Endpoint))
                {
                    tracerProviderBuilder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(observabilityConfiguration.OpenTelemetry.Endpoint);
                    });
                }
            })
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (observabilityConfiguration.OpenTelemetry?.Enabled == true && 
                    !string.IsNullOrWhiteSpace(observabilityConfiguration.OpenTelemetry.Endpoint))
                {
                    meterProviderBuilder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(observabilityConfiguration.OpenTelemetry.Endpoint);
                    });
                }
            });

        return builder;
    }
}
