using CityPark.Domain.Lots;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityPark.Infrastructure.Persistence.Configurations;

internal sealed class ParkingSpaceConfiguration : IEntityTypeConfiguration<ParkingSpace>
{
    // IsActive is part of the domain model (CP-009) but has no column yet. Ignoring it keeps the
    // model equal to the InitialCreate snapshot; the lot mapping adds the column with a migration.
    public void Configure(EntityTypeBuilder<ParkingSpace> builder)
    {
        builder.Ignore(s => s.IsActive);
    }
}
