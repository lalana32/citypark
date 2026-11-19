using CityPark.Domain.Lots;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

internal sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> builder)
    {
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
        builder.HasMany(l => l.Spaces)
            .WithOne()
            .HasForeignKey(ParkingSpaceConfiguration.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(l => l.Spaces).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
