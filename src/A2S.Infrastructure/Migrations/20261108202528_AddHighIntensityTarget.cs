using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHighIntensityTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HighIntensityMaxMinutes",
                table: "TrainingPlans",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "HighIntensityMinMinutes",
                table: "TrainingPlans",
                type: "integer",
                nullable: false,
                defaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighIntensityMaxMinutes",
                table: "TrainingPlans");

            migrationBuilder.DropColumn(
                name: "HighIntensityMinMinutes",
                table: "TrainingPlans");
        }
    }
}
