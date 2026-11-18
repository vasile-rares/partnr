using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Partnr.ServiceDefaults;

/// <summary>What every service of the backend starts with: telemetry and a health check. Business logic never goes in here.</summary>
public static class ServiceDefaultsExtensions
{
    public const string HealthPath = "/health";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments(HealthPath))
                .AddHttpClientInstrumentation()
                // Their own sources: Wolverine (outbox, handlers) and Npgsql (every query).
                .AddSource("Wolverine")
                .AddSource("Npgsql"));

        // Without a collector the telemetry stays in the process: the exporter is only on when OTEL_EXPORTER_OTLP_ENDPOINT is set
        // (the Aspire dashboard sets it for the projects it starts).
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Only "the process answers"; it says nothing about the database or the broker.
        builder.Services.AddHealthChecks();

        return builder;
    }

    /// <summary>Maps <c>/health</c>, open to anyone: the orchestrator asks it without a session.</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthPath).AllowAnonymous();

        return app;
    }
}
