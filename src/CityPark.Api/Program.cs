using System.Diagnostics;

using CityPark.Api;
using CityPark.Api.Auth;
using CityPark.Api.Errors;
using CityPark.Api.Health;
using CityPark.Infrastructure;
using CityPark.Infrastructure.Persistence.Seeding;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("CityPark")
    ?? throw new InvalidOperationException("Connection string 'CityPark' is not configured."));

builder.Services.AddCityParkAuthentication(builder.Environment);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// By default only Development throws on unreadable JSON; elsewhere the 400 would have no errors object.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag], timeout: TimeSpan.FromSeconds(5));

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
// Empty error responses, such as 404 for an unknown route, get a ProblemDetails body too.
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

// The document (/openapi/v1.json) is the public contract; the interactive UI is for developers only.
app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();
app.MapApiV1();

// Demo lots for local development; a no-op in every other environment.
await app.Services.SeedDevelopmentDataAsync(app.Environment);

app.Run();
