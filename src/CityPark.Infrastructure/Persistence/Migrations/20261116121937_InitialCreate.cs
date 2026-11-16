using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityPark.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

        migrationBuilder.CreateTable(
            name: "parking_spaces",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                label = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_parking_spaces", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "reservations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                space_id = table.Column<Guid>(type: "uuid", nullable: false),
                start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reservations", x => x.id);
                table.ForeignKey(
                    name: "fk_reservations_parking_spaces_space_id",
                    column: x => x.space_id,
                    principalTable: "parking_spaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_reservations_space_id",
            table: "reservations",
            column: "space_id");

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
        migrationBuilder.Sql("ALTER TABLE reservations DROP CONSTRAINT ex_reservations_space_no_overlap;");
        
        migrationBuilder.DropTable(
            name: "reservations");

        migrationBuilder.DropTable(
            name: "parking_spaces");
    }
}
