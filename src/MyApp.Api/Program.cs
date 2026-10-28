using MyApp.Api.Endpoints;
using MyApp.Application;
using MyApp.Infrastructure;

// Composition root: wiring only. No business logic in this project.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Default'."));

var app = builder.Build();

app.UseExceptionHandler(); // unexpected exceptions are bugs -> 500 ProblemDetails
app.MapOrderEndpoints();

await app.RunAsync();

// Top-level statements generate Program; the SDK decides its accessibility (it changed between previews). Pin it: the Api exports nothing.
internal sealed partial class Program
{
    private Program() { }
}
