using CityPark.Application.Persistence;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CityPark.Infrastructure.Persistence;

/// <summary>
/// CityPark tables plus the ASP.NET Core Identity tables with Guid keys (ADR-003), in one database
/// so a reservation can have a real foreign key to its user.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity configures its own tables, keys and indexes; it must run first.
        base.OnModelCreating(builder);

        // btree_gist lets a GiST index compare space_id with "=" in the no-overlap exclusion constraint.
        builder.HasPostgresExtension("btree_gist");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        RenameIdentityTables(builder);
    }

    // Identity names its tables and three indexes explicitly (AspNetUsers, UserNameIndex, ...), and the
    // snake_case convention never overrides an explicit name. Without this the schema would mix styles
    // and every hand-written query would need quoted identifiers ("AspNetUsers").
    private static void RenameIdentityTables(ModelBuilder builder)
    {
        builder.Entity<IdentityUser<Guid>>(user =>
        {
            user.ToTable("asp_net_users");
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_asp_net_users_normalized_user_name");
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_asp_net_users_normalized_email");
        });

        builder.Entity<IdentityRole<Guid>>(role =>
        {
            role.ToTable("asp_net_roles");
            role.HasIndex(r => r.NormalizedName).HasDatabaseName("ix_asp_net_roles_normalized_name");
        });

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims");
    }
}
