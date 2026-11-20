using System.Diagnostics;

using CityPark.Api.Errors;
using CityPark.Api.Health;
using CityPark.Infrastructure;
using CityPark.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("CityPark")
    ?? throw new InvalidOperationException("Connection string 'CityPark' is not configured."));

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag], timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

app.UseExceptionHandler();
// Empty error responses, such as 404 for an unknown route, get a ProblemDetails body too.
app.UseStatusCodePages();

app.MapHealthEndpoints();

// Demo lots for local development; a no-op in every other environment.
await app.Services.SeedDevelopmentDataAsync(app.Environment);

app.Run();
