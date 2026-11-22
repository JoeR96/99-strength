using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilityLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "SessionCompletions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowAlongUrl",
                table: "MobilityRoutines",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "MobilityRoutines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Stretch");

            migrationBuilder.AddColumn<string>(
                name: "ProgressionLog",
                table: "MobilityRoutines",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // Routines made before kinds existed: warm-ups by name, "yoga" ones a yoga flow (they already posted to Hevy as Yoga),
            // everything else a stretch.
            migrationBuilder.Sql(
                "UPDATE \"MobilityRoutines\" SET \"Kind\" = 'WarmUp' WHERE \"Name\" ILIKE '%mobility a%' OR \"Name\" ILIKE '%warm%';");
            migrationBuilder.Sql(
                "UPDATE \"MobilityRoutines\" SET \"Kind\" = 'YogaFlow' WHERE \"Kind\" = 'Stretch' AND \"Name\" ILIKE '%yoga%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "SessionCompletions");

            migrationBuilder.DropColumn(
                name: "FollowAlongUrl",
                table: "MobilityRoutines");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "MobilityRoutines");

            migrationBuilder.DropColumn(
                name: "ProgressionLog",
                table: "MobilityRoutines");
        }
    }
}
