using System;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EquipmentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MuscleGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsCompound = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultRepRangeMin = table.Column<int>(type: "integer", nullable: true),
                    DefaultRepRangeTarget = table.Column<int>(type: "integer", nullable: true),
                    DefaultRepRangeMax = table.Column<int>(type: "integer", nullable: true),
                    DefaultSets = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseDefinitions_EquipmentType",
                table: "ExerciseDefinitions",
                column: "EquipmentType");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseDefinitions_MuscleGroup",
                table: "ExerciseDefinitions",
                column: "MuscleGroup");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseDefinitions_Name",
                table: "ExerciseDefinitions",
                column: "Name",
                unique: true);

            // Add PendingWeightConfirmation and SuggestedWeight columns to ExerciseProgressions table
            migrationBuilder.AddColumn<bool>(
                name: "PendingWeightConfirmation",
                table: "ExerciseProgressions",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedWeightValue",
                table: "ExerciseProgressions",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedWeightUnit",
                table: "ExerciseProgressions",
                type: "text",
                nullable: true);

            // Seed exercise library data from embedded JSON resource
            SeedExerciseLibrary(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestedWeightUnit",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "SuggestedWeightValue",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "PendingWeightConfirmation",
                table: "ExerciseProgressions");

            migrationBuilder.DropTable(
                name: "ExerciseDefinitions");
        }

        private static void SeedExerciseLibrary(MigrationBuilder migrationBuilder)
        {
            var assembly = typeof(AddExerciseLibrary).Assembly;
            using var stream = assembly.GetManifestResourceStream("A2S.Infrastructure.SeedData.exercise-library.json");
            if (stream == null)
            {
                return;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var entries = JsonSerializer.Deserialize<List<ExerciseEntry>>(stream, options);
            if (entries == null)
            {
                return;
            }

            // Compound exercise muscle groups (exercises training multiple large muscle groups)
            var compoundMuscleGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "full body", "quadriceps", "hamstrings", "glutes", "back", "chest", "shoulders"
            };

            foreach (var entry in entries)
            {
                var muscleGroup = ParseMuscleGroup(entry.Description);
                var isCompound = compoundMuscleGroups.Contains(muscleGroup);

                migrationBuilder.InsertData(
                    table: "ExerciseDefinitions",
                    columns: new[] { "Id", "Name", "EquipmentType", "MuscleGroup", "IsCompound", "Description", "DefaultRepRangeMin", "DefaultRepRangeTarget", "DefaultRepRangeMax", "DefaultSets" },
                    values: new object[] {
                        Guid.NewGuid(),
                        entry.Name,
                        entry.Equipment,
                        muscleGroup,
                        isCompound,
                        entry.Description ?? "",
                        (object?)entry.DefaultRepRange?.Minimum ?? DBNull.Value,
                        (object?)entry.DefaultRepRange?.Target ?? DBNull.Value,
                        (object?)entry.DefaultRepRange?.Maximum ?? DBNull.Value,
                        (object?)entry.DefaultSets ?? DBNull.Value
                    });
            }
        }

        private static string ParseMuscleGroup(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "other";
            }

            // Format: "Hevy: <muscle_group>"
            const string prefix = "Hevy: ";
            if (description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return description[prefix.Length..].Trim().ToLowerInvariant();
            }

            return description.Trim().ToLowerInvariant();
        }

        private sealed record ExerciseEntry(
            string Name,
            string Equipment,
            RepRangeEntry? DefaultRepRange,
            int? DefaultSets,
            string? Description);

        private sealed record RepRangeEntry(int Minimum, int Target, int Maximum);
    }
}
