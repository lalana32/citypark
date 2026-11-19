using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace CityPark.Infrastructure.Tests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .Build();

    /// <summary>An Identity user that tests can use as the owner of reservations (foreign key I2).</summary>
    public Guid UserId { get; } = Guid.CreateVersion7();

    public AppDbContext CreateDbContext() =>
        new(AppDbContextOptions.Create(_container.GetConnectionString()));

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();

        db.Users.Add(new IdentityUser<Guid> { Id = UserId, UserName = "test-driver" });
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
