namespace MyApp.Application;

/// <summary>The name under which the use cases publish traces and metrics. A host subscribes to it: <c>AddSource(Telemetry.Name)</c>, <c>AddMeter(Telemetry.Name)</c>.</summary>
public static class Telemetry
{
    public const string Name = "MyApp.Application";
}
