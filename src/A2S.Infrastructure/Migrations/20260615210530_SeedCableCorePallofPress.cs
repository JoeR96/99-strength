using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCableCorePallofPress : Migration
    {
        // Deterministic id (MD5 of the name, matching ExerciseDefinitionSeeder.GenerateDeterministicId).
        private const string PallofId = "e336ebc6-d30f-2324-c88b-9b27cb1fdd61";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Cable Core Pallof Press" was used in the Big Daves Bonanza template but was
            // missing from exercise-library.json, so it never seeded. The create-workout
            // handler then dropped it, leaving an OrderInDay gap on Day 2/3 and failing with
            // "ordering must be sequential". Insert it idempotently — the seeder only runs on
            // an empty table, so existing databases need this migration to backfill the row.
            migrationBuilder.Sql(
                $"""
                INSERT INTO "ExerciseDefinitions"
                    ("Id", "Name", "EquipmentType", "MuscleGroup", "IsCompound", "Description", "DefaultRepRangeMin", "DefaultRepRangeMax", "DefaultSets")
                SELECT '{PallofId}', 'Cable Core Pallof Press', 'Machine', 'abdominals', FALSE, 'Hevy: abdominals', 8, 12, 3
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ExerciseDefinitions" WHERE "Name" = 'Cable Core Pallof Press'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"DELETE FROM \"ExerciseDefinitions\" WHERE \"Id\" = '{PallofId}';");
        }
    }
}
