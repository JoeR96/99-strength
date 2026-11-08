using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHevySync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HevyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LocalKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HevyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HevyLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HevyPullCursors",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastEventAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastPulledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HevyPullCursors", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "HevyWorkoutSnapshots",
                columns: table => new
                {
                    HevyWorkoutId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RoutineId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SessionCompletionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HevyWorkoutSnapshots", x => x.HevyWorkoutId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HevyLinks_UserId_Kind_HevyId",
                table: "HevyLinks",
                columns: new[] { "UserId", "Kind", "HevyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HevyLinks_UserId_Kind_LocalKey",
                table: "HevyLinks",
                columns: new[] { "UserId", "Kind", "LocalKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HevyWorkoutSnapshots_UserId_LocalDate",
                table: "HevyWorkoutSnapshots",
                columns: new[] { "UserId", "LocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HevyWorkoutSnapshots_UserId_Status",
                table: "HevyWorkoutSnapshots",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HevyLinks");

            migrationBuilder.DropTable(
                name: "HevyPullCursors");

            migrationBuilder.DropTable(
                name: "HevyWorkoutSnapshots");
        }
    }
}
