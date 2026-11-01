using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MyApp.Application.Behaviors;

/// <summary>One source and one meter for the whole assembly (a static field in the generic behavior would be one per request type).</summary>
internal static class ApplicationTelemetry
{
    public static readonly ActivitySource Source = new(Telemetry.Name);
    public static readonly Meter Meter = new(Telemetry.Name);

    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("myapp.requests", description: "Use cases handled, by request type and outcome (success, failure, error).");

    public static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("myapp.request.duration", unit: "ms", description: "Time to handle a use case.");
}
