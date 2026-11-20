using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CityPark.Api.Health;

public static class HealthEndpoints
{
    /// <summary>Checks with this tag decide readiness; liveness runs no checks.</summary>
    public const string ReadyTag = "ready";

    // Minimal API endpoints instead of MapHealthChecks, so they appear in the OpenAPI document.
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("/health").WithTags("Health");

        health.MapGet("/live", (HealthCheckService service, CancellationToken ct) =>
                CheckAsync(service, _ => false, ct))
            .WithSummary("The process is running and can answer requests.")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        health.MapGet("/ready", (HealthCheckService service, CancellationToken ct) =>
                CheckAsync(service, registration => registration.Tags.Contains(ReadyTag), ct))
            .WithSummary("The API can serve traffic: the database is reachable.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> CheckAsync(
        HealthCheckService service,
        Func<HealthCheckRegistration, bool> predicate,
        CancellationToken ct)
    {
        var report = await service.CheckHealthAsync(predicate, ct);

        // Only names and statuses: exception messages stay in the log.
        var body = new HealthResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()));

        var status = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return Results.Json(body, statusCode: status);
    }
}

public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, string> Checks);
