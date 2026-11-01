using MyApp.Application;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MyApp.Api.Hosting;

internal static class ObservabilityExtensions
{
    /// <summary>
    /// Traces, metrics and logs through OpenTelemetry: ASP.NET Core requests plus the use-case spans and metrics of the Application (<see cref="Telemetry.Name"/>).
    /// Nothing is exported until a collector is configured: set <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (and optionally <c>OTEL_SERVICE_NAME</c>).
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("MyApp.Api"))
            .WithTracing(t => t.AddAspNetCoreInstrumentation().AddSource(Telemetry.Name))
            .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddMeter(Telemetry.Name));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
