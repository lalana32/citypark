using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    // The no-overlap rule is a PostgreSQL EXCLUDE constraint (ex_reservations_space_no_overlap) created
    // with raw SQL in the InitialCreate migration; EF Core cannot model exclusion constraints (ADR-002).
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.Property(r => r.Status).HasConversion<short>();

        builder.HasOne(r => r.Space)
            .WithMany()
            .HasForeignKey(r => r.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Plate, Price and Currency are part of the domain model (CP-010) but have no columns yet.
        // Ignoring them keeps the model equal to the InitialCreate snapshot; the reservation mapping
        // adds the columns with a migration. Interval is computed from StartTime and EndTime.
        builder.Ignore(r => r.Plate);
        builder.Ignore(r => r.Price);
        builder.Ignore(r => r.Currency);
        builder.Ignore(r => r.Interval);
    }
}
