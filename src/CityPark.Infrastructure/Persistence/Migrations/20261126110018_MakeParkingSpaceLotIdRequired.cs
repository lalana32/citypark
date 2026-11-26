using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityPark.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeParkingSpaceLotIdRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "lot_id",
                table: "parking_spaces",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "lot_id",
                table: "parking_spaces",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
