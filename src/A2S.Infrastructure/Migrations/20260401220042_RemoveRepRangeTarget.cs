using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRepRangeTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultRepRangeTarget",
                table: "ExerciseDefinitions");

            migrationBuilder.RenameColumn(
                name: "RepRangeTarget",
                table: "ExerciseProgressions",
                newName: "Tier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "ExerciseProgressions",
                newName: "RepRangeTarget");

            migrationBuilder.AddColumn<int>(
                name: "DefaultRepRangeTarget",
                table: "ExerciseDefinitions",
                type: "integer",
                nullable: true);
        }
    }
}
