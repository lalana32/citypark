using CityPark.Domain.Lots;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

internal sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> builder)
    {
        // The domain assigns Guid v7 keys itself. EF Core's default for Guid keys is "generated on add",
        // which makes it treat any entity found with a non-empty key as an existing row.
        builder.Property(l => l.Id).ValueGeneratedNever();

        // Rate and OpeningHours are value objects without identity: complex properties store them as
        // columns of parking_lots instead of separate tables (product brief, section 8).
        builder.ComplexProperty(l => l.Rate, rate =>
        {
            rate.Property(r => r.HourlyPrice).HasColumnName("hourly_price").HasPrecision(10, 2);
            rate.Property(r => r.Currency).HasColumnName("currency");
        });

        builder.ComplexProperty(l => l.OpeningHours, hours =>
        {
            hours.Property(h => h.OpensAt).HasColumnName("opens_at");
            hours.Property(h => h.ClosesAt).HasColumnName("closes_at");
        });

        // The domain exposes Spaces read-only; EF Core loads and saves through the _spaces field.
        // LotId is a shadow property: the database needs the column, the domain does not.
        // Required (I1): by convention EF Core makes a shadow foreign key nullable.
        builder.HasMany(l => l.Spaces)
            .WithOne()
            .HasForeignKey(ParkingSpaceConfiguration.LotId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(l => l.Spaces).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
