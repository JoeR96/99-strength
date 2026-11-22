using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StartGate",
                table: "CardioTracks",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "ReadinessEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Colour = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    KeepFinisher = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessEntries_UserId_Date",
                table: "ReadinessEntries",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadinessEntries");

            migrationBuilder.DropColumn(
                name: "StartGate",
                table: "CardioTracks");
        }
    }
}
