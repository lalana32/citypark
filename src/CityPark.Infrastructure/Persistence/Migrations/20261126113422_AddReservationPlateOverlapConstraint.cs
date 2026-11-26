using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityPark.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// I5 (ADR-002): Active reservations of one plate never overlap, in any lot, using half-open
    /// [start, end). Raw SQL like AddReservationOverlapConstraint; the model snapshot does not change.
    /// </summary>
    public partial class AddReservationPlateOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fails if existing Active rows of one plate already overlap; clean them up first.
            migrationBuilder.Sql("""
                ALTER TABLE reservations
                ADD CONSTRAINT ex_reservations_plate_no_overlap
                EXCLUDE USING gist (
                    plate WITH =,
                    tstzrange(start_time, end_time, '[)') WITH &&
                ) WHERE (status = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE reservations DROP CONSTRAINT ex_reservations_plate_no_overlap;");
        }
    }
}
