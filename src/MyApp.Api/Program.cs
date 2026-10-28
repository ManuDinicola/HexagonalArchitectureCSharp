using MyApp.Api.Endpoints;
using MyApp.Application;
using MyApp.Infrastructure;

// Composition root: wiring only. No business logic in this project.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default")!);

var app = builder.Build();

app.UseExceptionHandler(); // unexpected exceptions are bugs -> 500 ProblemDetails
app.MapOrderEndpoints();

app.Run();
