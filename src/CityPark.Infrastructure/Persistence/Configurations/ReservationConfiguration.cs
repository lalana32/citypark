using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.Property(r => r.Status).HasConversion<short>();

        builder.HasOne(r => r.Space)
            .WithMany()
            .HasForeignKey(r => r.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
