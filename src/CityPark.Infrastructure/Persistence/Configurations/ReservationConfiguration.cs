using CityPark.Domain.Reservations;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    private const string VersionProperty = "Version";

    // The no-overlap rules are PostgreSQL EXCLUDE constraints created with raw SQL, because EF Core
    // cannot model them (ADR-002): ex_reservations_space_no_overlap (R6, AddReservationOverlapConstraint)
    // and ex_reservations_plate_no_overlap (R7, AddReservationPlateOverlapConstraint).
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        // Assigned by Reservation.Create (Guid v7), never by EF Core or the database.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Status).HasConversion<short>();

        // Stored as the normalized string; reading it back runs the LicensePlate checks again.
        builder.Property(r => r.Plate).HasConversion(plate => plate.Value, value => new LicensePlate(value));

        builder.Property(r => r.Price).HasPrecision(10, 2);

        // Reservations are history: deleting a space or a user must not silently delete them (I2).
        builder.HasOne(r => r.Space)
            .WithMany()
            .HasForeignKey(r => r.SpaceId)
            .OnDelete(DeleteBehavior.Restrict)
            // Explicit: the naming convention builds this name before ParkingSpaceConfiguration renames
            // the table, which gave fk_reservations_parking_space_space_id.
            .HasConstraintName("fk_reservations_parking_spaces_space_id");

        builder.HasOne<IdentityUser<Guid>>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Computed from StartTime and EndTime.
        builder.Ignore(r => r.Interval);

        // Optimistic concurrency on PostgreSQL's xmin system column, which changes on every update of the row.
        // A shadow property, so the domain does not know about it; no real column is added.
        builder.Property<uint>(VersionProperty)
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
