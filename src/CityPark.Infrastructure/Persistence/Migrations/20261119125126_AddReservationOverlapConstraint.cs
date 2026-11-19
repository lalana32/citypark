using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityPark.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// I4 (ADR-002): Active reservations of one space never overlap, using half-open [start, end).
    /// EF Core cannot model exclusion constraints, so this migration is raw SQL and the model
    /// snapshot does not change. The plate constraint (I5) follows in a later card.
    /// </summary>
    public partial class AddReservationOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // InitialCreate already creates the extension (HasPostgresExtension in AppDbContext);
            // IF NOT EXISTS keeps this migration correct on its own and harmless when run after it.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // Fails if existing Active rows already overlap: PostgreSQL validates every row
            // when the constraint is added, so such data must be cleaned up first.
            migrationBuilder.Sql("""
                ALTER TABLE reservations
                ADD CONSTRAINT ex_reservations_space_no_overlap
                EXCLUDE USING gist (
                    space_id WITH =,
                    tstzrange(start_time, end_time, '[)') WITH &&
                ) WHERE (status = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the constraint: the extension belongs to the model and to InitialCreate.
            migrationBuilder.Sql("ALTER TABLE reservations DROP CONSTRAINT ex_reservations_space_no_overlap;");
        }
    }
}
