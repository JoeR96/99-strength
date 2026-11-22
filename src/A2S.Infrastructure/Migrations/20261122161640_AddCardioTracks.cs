using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCardioTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeartRateZonePercents",
                table: "Users",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxHeartRate",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PushRoutinesToHevy",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CardioTrackId",
                table: "SessionCompletions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardioTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Modality = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Benchmark = table.Column<string>(type: "jsonb", nullable: true),
                    State = table.Column<string>(type: "jsonb", nullable: false),
                    LastSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    Audit = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Benchmarks = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Sessions = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Stages = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardioTracks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardioTracks_UserId",
                table: "CardioTracks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardioTracks");

            migrationBuilder.DropColumn(
                name: "HeartRateZonePercents",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MaxHeartRate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PushRoutinesToHevy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CardioTrackId",
                table: "SessionCompletions");
        }
    }
}
