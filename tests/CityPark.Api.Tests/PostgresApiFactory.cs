using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

namespace CityPark.Api.Tests;

/// <summary>The API connected to a real PostgreSQL 17 container, migrated before the API starts.</summary>
public class PostgresApiFactory : ApiFactory, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    protected override string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // The API does not migrate on start; the Identity tables and roles must exist first.
        await using var db = new AppDbContext(AppDbContextOptions.Create(ConnectionString));
        await db.Database.MigrateAsync();
    }

    /// <summary>Creates a user directly, for roles that registration never gives, such as Admin.</summary>
    public async Task CreateUserAsync(string email, string password, string role)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var user = new IdentityUser<Guid> { Id = Guid.CreateVersion7(), UserName = email, Email = email };

        EnsureSucceeded(await users.CreateAsync(user, password));
        EnsureSucceeded(await users.AddToRoleAsync(user, role));
    }

    public async Task<bool> IsLockedOutAsync(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException($"No user {email}.");

        return await users.IsLockedOutAsync(user);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Code)));
        }
    }
}
