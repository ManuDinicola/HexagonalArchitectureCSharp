using MyApp.Api.Endpoints;
using MyApp.Api.Hosting;
using MyApp.Application;
using MyApp.Infrastructure;

// Composition root: wiring only. No business logic in this project.
var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.Services.AddProblemDetails();
builder.Services.AddApiDocs();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Default'."),
    migrateOnStartup: builder.Configuration.GetValue("Database:MigrateOnStartup", true),
    runBackgroundJobs: builder.Configuration.GetValue("BackgroundJobs:Enabled", true),
    outboxPollInterval: TimeSpan.FromSeconds(builder.Configuration.GetValue("Outbox:PollIntervalSeconds", 2.0)),
    retention: TimeSpan.FromDays(builder.Configuration.GetValue("Retention:Days", 7.0)));

var app = builder.Build();

app.UseExceptionHandler(); // unexpected exceptions are bugs -> 500 ProblemDetails
app.MapApiDocs();
app.MapHealthEndpoints();
app.MapOrderEndpoints();

await app.RunAsync();

// Top-level statements generate Program; the SDK decides its accessibility (it changed between previews). Pin it: the Api exports nothing.
internal sealed partial class Program
{
    private Program() { }
}
