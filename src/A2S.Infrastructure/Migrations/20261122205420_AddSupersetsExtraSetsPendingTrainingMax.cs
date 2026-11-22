using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupersetsExtraSetsPendingTrainingMax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraSetSide",
                table: "Exercises",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "SupersetGroup",
                table: "Exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrainingMaxPending",
                table: "ExerciseProgressions",
                type: "boolean",
                nullable: true,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraSetSide",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "SupersetGroup",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "IsTrainingMaxPending",
                table: "ExerciseProgressions");
        }
    }
}
