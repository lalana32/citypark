using CityPark.Application.Common;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CityPark.Infrastructure.Tests.Persistence;

public sealed class IdentityRoleSeedTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migration_InsertsCustomerAndAdminRoles()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateDbContext();

        var roles = await db.Roles.OrderBy(r => r.Name).ToListAsync(ct);

        Assert.Equal([AppRoles.Admin, AppRoles.Customer], roles.Select(r => r.Name));
    }

    // UserManager and RoleManager find a role by its normalized name, so it must be the one they compute.
    [Fact]
    public async Task SeededRoles_HaveTheNormalizedNameIdentityLooksUp()
    {
        var ct = TestContext.Current.CancellationToken;
        var normalizer = new UpperInvariantLookupNormalizer();
        await using var db = fixture.CreateDbContext();

        var roles = await db.Roles.ToListAsync(ct);

        Assert.NotEmpty(roles);
        Assert.All(roles, role => Assert.Equal(normalizer.NormalizeName(role.Name), role.NormalizedName));
    }
}
