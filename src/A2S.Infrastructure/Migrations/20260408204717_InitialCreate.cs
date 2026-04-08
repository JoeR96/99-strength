using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DefaultRepRangeMin = table.Column<int>(type: "integer", nullable: true),
                    DefaultRepRangeMax = table.Column<int>(type: "integer", nullable: true),
                    DefaultSets = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Variant = table.Column<string>(type: "text", nullable: false),
                    TotalWeeks = table.Column<int>(type: "integer", nullable: false),
                    CurrentWeek = table.Column<int>(type: "integer", nullable: false),
                    CurrentBlock = table.Column<int>(type: "integer", nullable: false),
                    CurrentDay = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BlockSequence = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[1,2,3]'::jsonb"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ArchivedActivities = table.Column<string>(type: "jsonb", nullable: true),
                    AuditEntries = table.Column<string>(type: "jsonb", nullable: true),
                    CompletedActivities = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Equipment = table.Column<string>(type: "text", nullable: false),
                    AssignedDay = table.Column<string>(type: "text", nullable: false),
                    OrderInDay = table.Column<int>(type: "integer", nullable: false),
                    HevyExerciseTemplateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkoutId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exercises_Workouts_WorkoutId",
                        column: x => x.WorkoutId,
                        principalTable: "Workouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseProgressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgressionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingMaxValue = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    TrainingMaxUnit = table.Column<string>(type: "text", nullable: true),
                    UseAmrap = table.Column<bool>(type: "boolean", nullable: true),
                    BaseSetsPerExercise = table.Column<int>(type: "integer", nullable: true),
                    Tier = table.Column<int>(type: "integer", nullable: true),
                    MinimalSets_CurrentWeightValue = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    MinimalSets_CurrentWeightUnit = table.Column<string>(type: "text", nullable: true),
                    MinimalSets_TargetTotalReps = table.Column<int>(type: "integer", nullable: true),
                    MinimalSets_CurrentSetCount = table.Column<int>(type: "integer", nullable: true),
                    MinimalSets_StartingSets = table.Column<int>(type: "integer", nullable: true),
                    MinimalSets_MinimumSets = table.Column<int>(type: "integer", nullable: true),
                    MinimalSets_MaximumSets = table.Column<int>(type: "integer", nullable: true),
                    MinimalSets_Equipment = table.Column<string>(type: "text", nullable: true),
                    RepRangeMinimum = table.Column<int>(type: "integer", nullable: true),
                    RepRangeMaximum = table.Column<int>(type: "integer", nullable: true),
                    CurrentSetCount = table.Column<int>(type: "integer", nullable: true),
                    StartingSets = table.Column<int>(type: "integer", nullable: true),
                    TargetSets = table.Column<int>(type: "integer", nullable: true),
                    CurrentWeightValue = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    CurrentWeightUnit = table.Column<string>(type: "text", nullable: true),
                    Equipment = table.Column<string>(type: "text", nullable: true),
                    IsUnilateral = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    PendingWeightConfirmation = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    SuggestedWeightValue = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    SuggestedWeightUnit = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseProgressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseProgressions_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseProgressions_ExerciseId",
                table: "ExerciseProgressions",
                column: "ExerciseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_WorkoutId",
                table: "Exercises",
                column: "WorkoutId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workouts_UserId",
                table: "Workouts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Workouts_UserId_Status",
                table: "Workouts",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseDefinitions");

            migrationBuilder.DropTable(
                name: "ExerciseProgressions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Exercises");

            migrationBuilder.DropTable(
                name: "Workouts");
        }
    }
}
