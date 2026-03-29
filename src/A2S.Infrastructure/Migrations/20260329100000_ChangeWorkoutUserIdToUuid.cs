using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeWorkoutUserIdToUuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All existing UserId values are valid GUID strings (stored via Guid.ToString()).
            // Convert the text column to uuid using PostgreSQL's CAST.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Workouts"
                    ALTER COLUMN "UserId" TYPE uuid
                    USING "UserId"::uuid;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Workouts"
                    ALTER COLUMN "UserId" TYPE text
                    USING "UserId"::text;
                """);
        }
    }
}
