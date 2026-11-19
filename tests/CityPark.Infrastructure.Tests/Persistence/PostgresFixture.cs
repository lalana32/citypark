using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

using Npgsql;

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

    /// <summary>
    /// A context that passes every SQL command it executes to <paramref name="log"/>, for tests that
    /// check what EF Core really sends to PostgreSQL.
    /// </summary>
    public AppDbContext CreateLoggingDbContext(Action<string> log)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(builder, _container.GetConnectionString());
        builder.LogTo(log, [RelationalEventId.CommandExecuted], LogLevel.Information);
        return new AppDbContext(builder.Options);
    }

    /// <summary>
    /// A context for another database on the same server. EF Core creates the database on the first
    /// migration, so tests that move the schema up and down get one of their own and leave the
    /// shared, fully migrated database untouched.
    /// </summary>
    public AppDbContext CreateDbContext(string database) =>
        new(AppDbContextOptions.Create(GetConnectionString(database)));

    /// <summary>Connection string for another database on the same server, for code that builds its own context.</summary>
    public string GetConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
        }.ConnectionString;

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
