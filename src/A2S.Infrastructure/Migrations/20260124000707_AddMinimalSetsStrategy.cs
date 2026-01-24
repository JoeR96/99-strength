using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimalSetsStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUnilateral",
                table: "ExerciseProgressions",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinimalSets_CurrentSetCount",
                table: "ExerciseProgressions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinimalSets_CurrentWeightUnit",
                table: "ExerciseProgressions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimalSets_CurrentWeightValue",
                table: "ExerciseProgressions",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinimalSets_Equipment",
                table: "ExerciseProgressions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimalSets_MaximumSets",
                table: "ExerciseProgressions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimalSets_MinimumSets",
                table: "ExerciseProgressions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimalSets_StartingSets",
                table: "ExerciseProgressions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimalSets_TargetTotalReps",
                table: "ExerciseProgressions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUnilateral",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_CurrentSetCount",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_CurrentWeightUnit",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_CurrentWeightValue",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_Equipment",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_MaximumSets",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_MinimumSets",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_StartingSets",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "MinimalSets_TargetTotalReps",
                table: "ExerciseProgressions");
        }
    }
}
