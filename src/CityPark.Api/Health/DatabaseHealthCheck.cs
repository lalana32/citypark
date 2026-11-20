using CityPark.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CityPark.Api.Health;

/// <summary>Healthy when PostgreSQL answers <c>SELECT 1</c>.</summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // The plain connection, not CanConnectAsync: EF Core's retry strategy (ADR-002) would keep a
        // failing check busy for many seconds. An exception here is reported as Unhealthy by the
        // health check service.
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
