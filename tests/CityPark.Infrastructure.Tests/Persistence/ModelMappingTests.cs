using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CityPark.Infrastructure.Tests.Persistence;

// Checks the EF Core model only; no database is needed. The migration is checked in the Postgres tests.
public class ModelMappingTests
{
    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        using var db = new AppDbContext(AppDbContextOptions.Create("Host=unused"));
        return db.Model;
    }

    private static IEntityType Entity<T>() => Model.FindEntityType(typeof(T))!;

    [Fact]
    public void Reservation_IsMappedToSnakeCaseTableAndColumns()
    {
        var entity = Entity<Reservation>();
        var table = StoreObjectIdentifier.Table("reservations");

        Assert.Equal("reservations", entity.GetTableName());
        Assert.Equal("space_id", entity.FindProperty(nameof(Reservation.SpaceId))!.GetColumnName(table));
        Assert.Equal("user_id", entity.FindProperty(nameof(Reservation.UserId))!.GetColumnName(table));
        Assert.Equal("start_time", entity.FindProperty(nameof(Reservation.StartTime))!.GetColumnName(table));
        Assert.Equal("plate", entity.FindProperty(nameof(Reservation.Plate))!.GetColumnName(table));
    }

    // Identity sets its table and index names explicitly, so the naming convention alone is not enough.
    [Fact]
    public void EveryTableAndIndexName_IsSnakeCase()
    {
        var names = Model.GetEntityTypes()
            .SelectMany(e => e.GetIndexes().Select(i => i.GetDatabaseName()!).Append(e.GetTableName()!))
            .ToList();

        Assert.All(names, name => Assert.Matches("^[a-z][a-z0-9_]*$", name));
        Assert.Contains("parking_spaces", names);
        Assert.Contains("asp_net_users", names);
    }

    [Fact]
    public void EveryDateTimeProperty_IsTimestamptz()
    {
        var instants = Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => IsInstant(Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType))
            .ToList();

        // Reservation start and end, plus Identity's LockoutEnd.
        Assert.True(instants.Count >= 3);
        Assert.All(instants, p => Assert.Equal("timestamp with time zone", p.GetColumnType()));
    }

    private static bool IsInstant(Type type) => type == typeof(DateTime) || type == typeof(DateTimeOffset);

    [Fact]
    public void MoneyColumns_AreNumeric10_2()
    {
        var hourlyPrice = Entity<ParkingLot>()
            .FindComplexProperty(nameof(ParkingLot.Rate))!.ComplexType
            .FindProperty(nameof(Rate.HourlyPrice))!;

        Assert.Equal("numeric(10,2)", hourlyPrice.GetColumnType());
        Assert.Equal("numeric(10,2)", Entity<Reservation>().FindProperty(nameof(Reservation.Price))!.GetColumnType());
    }

    [Fact]
    public void ParkingLot_StoresRateAndOpeningHoursAsColumnsOfTheLot()
    {
        var lot = Entity<ParkingLot>();
        var table = StoreObjectIdentifier.Table("parking_lots");

        string Column(string complex, string property) =>
            lot.FindComplexProperty(complex)!.ComplexType.FindProperty(property)!.GetColumnName(table)!;

        Assert.Equal("hourly_price", Column(nameof(ParkingLot.Rate), nameof(Rate.HourlyPrice)));
        Assert.Equal("currency", Column(nameof(ParkingLot.Rate), nameof(Rate.Currency)));
        Assert.Equal("opens_at", Column(nameof(ParkingLot.OpeningHours), nameof(OpeningHours.OpensAt)));
        Assert.Equal("closes_at", Column(nameof(ParkingLot.OpeningHours), nameof(OpeningHours.ClosesAt)));
    }

    [Fact]
    public void ParkingSpace_LabelIsUniquePerLot()
    {
        var index = Assert.Single(Entity<ParkingSpace>().GetIndexes(), i => i.IsUnique);

        Assert.Equal(["LotId", nameof(ParkingSpace.Label)], index.Properties.Select(p => p.Name));
        Assert.Equal("ix_parking_spaces_lot_id_label", index.GetDatabaseName());
    }

    // I1: a space belongs to exactly one lot. A nullable lot_id would also slip past the unique index.
    [Fact]
    public void ParkingSpace_LotId_IsRequired()
    {
        var foreignKey = Assert.Single(Entity<ParkingSpace>().GetForeignKeys());

        Assert.True(foreignKey.IsRequired);
        Assert.False(Entity<ParkingSpace>().FindProperty("LotId")!.IsNullable);
    }

    [Fact]
    public void ParkingSpace_IsActive_IsMapped()
    {
        Assert.NotNull(Entity<ParkingSpace>().FindProperty(nameof(ParkingSpace.IsActive)));
    }

    [Fact]
    public void Reservation_HasRestrictForeignKeysToSpaceAndUser()
    {
        var foreignKeys = Entity<Reservation>().GetForeignKeys()
            .ToDictionary(fk => fk.PrincipalEntityType.GetTableName()!);

        Assert.Equal(["asp_net_users", "parking_spaces"], foreignKeys.Keys.Order());
        Assert.All(foreignKeys.Values, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Equal(nameof(Reservation.UserId), Assert.Single(foreignKeys["asp_net_users"].Properties).Name);
        Assert.Equal("fk_reservations_parking_spaces_space_id", foreignKeys["parking_spaces"].GetConstraintName());
        Assert.Equal("fk_reservations_asp_net_users_user_id", foreignKeys["asp_net_users"].GetConstraintName());
    }

    [Theory]
    [InlineData(typeof(ParkingLot))]
    [InlineData(typeof(ParkingSpace))]
    [InlineData(typeof(Reservation))]
    public void DomainKeys_AreNeverGeneratedByEfCore(Type type)
    {
        var key = Assert.Single(Model.FindEntityType(type)!.FindPrimaryKey()!.Properties);

        Assert.Equal(ValueGenerated.Never, key.ValueGenerated);
    }

    [Fact]
    public void Reservation_Interval_IsNotMapped()
    {
        Assert.Null(Entity<Reservation>().FindProperty(nameof(Reservation.Interval)));
    }

    [Fact]
    public void Reservation_UsesXminShadowPropertyAsConcurrencyToken()
    {
        var version = Entity<Reservation>().FindProperty("Version")!;

        Assert.True(version.IsShadowProperty());
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName(StoreObjectIdentifier.Table("reservations")));
        Assert.Equal("xid", version.GetColumnType());
    }
}
