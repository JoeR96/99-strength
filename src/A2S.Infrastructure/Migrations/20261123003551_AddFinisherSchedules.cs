using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinisherSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinisherSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClientSessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Nudges = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinisherSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinisherSchedules_NextDueAtUtc",
                table: "FinisherSchedules",
                column: "NextDueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FinisherSchedules_UserId_ClientSessionId",
                table: "FinisherSchedules",
                columns: new[] { "UserId", "ClientSessionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinisherSchedules");
        }
    }
}
