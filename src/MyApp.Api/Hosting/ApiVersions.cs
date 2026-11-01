namespace MyApp.Api.Hosting;

/// <summary>
/// URL-segment versioning: every endpoint lives under <c>/v1</c>. A breaking change gets a <c>v2</c> group next to it
/// (and its own OpenAPI document) while v1 keeps working until it is retired. Additive changes (a new field, a new endpoint) stay in the same version.
/// </summary>
internal static class ApiVersions
{
    public const string V1 = "v1";
}
