using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CityPark.Infrastructure.Tests.Persistence;

public class ModelMappingTests
{
    private static IEntityType ReservationEntity()
    {
        using var db = new CityParkDbContext(CityParkDbContextOptions.Create("Host=unused"));
        return db.Model.FindEntityType(typeof(Reservation))!;
    }

    [Fact]
    public void Reservation_IsMappedToSnakeCaseTableAndColumns()
    {
        var entity = ReservationEntity();
        var table = StoreObjectIdentifier.Table("reservations");

        Assert.Equal("reservations", entity.GetTableName());
        Assert.Equal("space_id", entity.FindProperty(nameof(Reservation.SpaceId))!.GetColumnName(table));
        Assert.Equal("start_time", entity.FindProperty(nameof(Reservation.StartTime))!.GetColumnName(table));
        Assert.Equal("end_time", entity.FindProperty(nameof(Reservation.EndTime))!.GetColumnName(table));
    }

    [Fact]
    public void Reservation_StoresTimesAsTimestamptzAndStatusAsSmallint()
    {
        var entity = ReservationEntity();

        Assert.Equal("timestamp with time zone", entity.FindProperty(nameof(Reservation.StartTime))!.GetColumnType());
        Assert.Equal("smallint", entity.FindProperty(nameof(Reservation.Status))!.GetColumnType());
    }

    [Fact]
    public void ParkingSpace_IsActive_IsNotMappedUntilTheLotMapping()
    {
        using var db = new CityParkDbContext(CityParkDbContextOptions.Create("Host=unused"));
        var entity = db.Model.FindEntityType(typeof(ParkingSpace))!;

        Assert.Null(entity.FindProperty(nameof(ParkingSpace.IsActive)));
    }

    [Theory]
    [InlineData(nameof(Reservation.Plate))]
    [InlineData(nameof(Reservation.Price))]
    [InlineData(nameof(Reservation.Currency))]
    [InlineData(nameof(Reservation.Interval))]
    public void Reservation_DomainOnlyMembers_AreNotMappedUntilTheReservationMapping(string member)
    {
        var entity = ReservationEntity();

        Assert.Null(entity.FindProperty(member));
        Assert.Null(entity.FindNavigation(member));
    }

    [Fact]
    public void DeletingASpace_DoesNotCascadeToReservations()
    {
        var foreignKey = Assert.Single(ReservationEntity().GetForeignKeys());

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}
