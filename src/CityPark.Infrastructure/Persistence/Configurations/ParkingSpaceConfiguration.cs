using CityPark.Domain.Lots;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

internal sealed class ParkingSpaceConfiguration : IEntityTypeConfiguration<ParkingSpace>
{
    /// <summary>Shadow foreign key to parking_lots: the database needs the column, the domain does not.</summary>
    internal const string LotId = "LotId";

    public void Configure(EntityTypeBuilder<ParkingSpace> builder)
    {
        // Explicit because there is no DbSet<ParkingSpace> to name the table after (spaces live in the lot).
        builder.ToTable("parking_spaces");

        // Assigned by the domain. Without this, a space added to an already loaded lot would be
        // tracked as Modified (its key is set), and EF Core would send an UPDATE instead of an INSERT.
        builder.Property(s => s.Id).ValueGeneratedNever();

        // I1: ParkingLot.AddSpace already rejects a duplicate label, but only for the spaces it has
        // loaded. The unique index also holds for concurrent requests and for direct SQL.
        builder.HasIndex(LotId, nameof(ParkingSpace.Label)).IsUnique();
    }
}
