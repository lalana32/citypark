using CityPark.Application.Common;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

/// <summary>The two roles are fixed data, so a migration inserts them rather than the app at startup.</summary>
internal sealed class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public static readonly Guid CustomerRoleId = new("0193a1c0-0000-7000-8000-00000000c001");

    public static readonly Guid AdminRoleId = new("0193a1c0-0000-7000-8000-00000000a001");

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        // Every value is a constant: IdentityRole defaults ConcurrencyStamp to a new Guid, which would
        // make each "migrations add" see a changed row.
        builder.HasData(
            Role(CustomerRoleId, AppRoles.Customer),
            Role(AdminRoleId, AppRoles.Admin));
    }

    private static IdentityRole<Guid> Role(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id.ToString(),
    };
}
