using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardioProtocols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    WeekIndex = table.Column<int>(type: "integer", nullable: true),
                    RepeatWeek = table.Column<bool>(type: "boolean", nullable: false),
                    Intervals = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardioProtocols", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MobilityRoutines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    Drills = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilityRoutines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedWorkoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    FlexibleMealsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    AlcoholOccasionsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    AllowanceUses = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Checkpoints = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Phases = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Schedule = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LiftDay = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CardioProtocolId = table.Column<Guid>(type: "uuid", nullable: true),
                    MobilityRoutineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HevyWorkoutId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TrainingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DrillTicks = table.Column<string>(type: "jsonb", nullable: true),
                    Cardio = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionCompletions_TrainingPlans_TrainingPlanId",
                        column: x => x.TrainingPlanId,
                        principalTable: "TrainingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardioProtocols_UserId",
                table: "CardioProtocols",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilityRoutines_UserId",
                table: "MobilityRoutines",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCompletions_TrainingPlanId_Date",
                table: "SessionCompletions",
                columns: new[] { "TrainingPlanId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionCompletions_TrainingPlanId_HevyWorkoutId",
                table: "SessionCompletions",
                columns: new[] { "TrainingPlanId", "HevyWorkoutId" },
                unique: true,
                filter: "\"HevyWorkoutId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlans_UserId_IsActive",
                table: "TrainingPlans",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardioProtocols");

            migrationBuilder.DropTable(
                name: "MobilityRoutines");

            migrationBuilder.DropTable(
                name: "SessionCompletions");

            migrationBuilder.DropTable(
                name: "TrainingPlans");
        }
    }
}
