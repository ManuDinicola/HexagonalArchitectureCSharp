using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace MyApp.Api.Endpoints;

internal static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    /// <summary>
    /// <c>/health/live</c>: the process answers (no checks, so a database outage never restarts it).
    /// <c>/health/ready</c>: every check tagged "ready" passes (the database); a load balancer takes the instance out of rotation when it fails.
    /// </summary>
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
    }
}
