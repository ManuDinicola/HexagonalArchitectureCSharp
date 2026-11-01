using Scalar.AspNetCore;

namespace MyApp.Api.Hosting;

internal static class ApiDocsExtensions
{
    public static IServiceCollection AddApiDocs(this IServiceCollection services) =>
        services.AddOpenApi(ApiVersions.V1);

    /// <summary>The OpenAPI document (<c>/openapi/v1.json</c>) and the Scalar reference (<c>/scalar</c>). Development only: a production API does not advertise itself.</summary>
    public static WebApplication MapApiDocs(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}
