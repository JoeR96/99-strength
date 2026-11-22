using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHevyWorkoutPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HevyWorkoutPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClientSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HevyWorkoutId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HevyWorkoutPosts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HevyWorkoutPosts_Status_NextAttemptAtUtc",
                table: "HevyWorkoutPosts",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HevyWorkoutPosts_UserId_ClientSessionId",
                table: "HevyWorkoutPosts",
                columns: new[] { "UserId", "ClientSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HevyWorkoutPosts_UserId_LocalDate",
                table: "HevyWorkoutPosts",
                columns: new[] { "UserId", "LocalDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HevyWorkoutPosts");
        }
    }
}
