using CityPark.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace CityPark.Infrastructure.Tests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .Build();

    public CityParkDbContext CreateDbContext() =>
        new(CityParkDbContextOptions.Create(_container.GetConnectionString()));

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
