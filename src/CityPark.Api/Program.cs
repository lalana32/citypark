using System.Diagnostics;

using CityPark.Api;
using CityPark.Api.Admin;
using CityPark.Api.Auth;
using CityPark.Api.Errors;
using CityPark.Api.Health;
using CityPark.Api.Lots;
using CityPark.Api.Reservations;
using CityPark.Application;
using CityPark.Infrastructure;
using CityPark.Infrastructure.Identity;
using CityPark.Infrastructure.Persistence.Seeding;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("CityPark")
    ?? throw new InvalidOperationException("Connection string 'CityPark' is not configured."));
builder.Services.AddApplication();

// HTTPS only, except in Development (Vite proxy) or with Cookies__AllowHttp (local Docker stack).
var cookieSecurePolicy = CookieSecurity.PolicyFor(builder.Environment, builder.Configuration);
builder.Services.AddCityParkAuthentication(cookieSecurePolicy);
builder.Services.AddCityParkCsrfProtection(cookieSecurePolicy);
builder.Services.AddCityParkRateLimiting();

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
// After routing (implicit at the start), so it sees each endpoint's policy.
app.UseRateLimiter();

// The document (/openapi/v1.json) is the public contract; the interactive UI is for developers only.
app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();
app.MapApiV1()
    .MapAuthenticationEndpoints()
    .MapLotEndpoints()
    .MapReservationEndpoints()
    .MapAdminEndpoints();

// Demo lots for local development; a no-op in every other environment.
await app.Services.SeedDevelopmentDataAsync(app.Environment);

// Every environment; skipped when the Admin__Email and Admin__Password variables are not set.
await app.Services.SeedAdminAsync(app.Configuration[AdminSeeder.EmailKey], app.Configuration[AdminSeeder.PasswordKey]);

app.Run();
