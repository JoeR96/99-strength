using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCardioStageProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardioStage_CoolDown",
                table: "ExerciseProgressions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardioStage_Lane",
                table: "ExerciseProgressions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CardioStage_RequiresSpeed",
                table: "ExerciseProgressions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardioStage_Stages",
                table: "ExerciseProgressions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardioStage_State",
                table: "ExerciseProgressions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardioStage_WarmUp",
                table: "ExerciseProgressions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardioStage_CoolDown",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "CardioStage_Lane",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "CardioStage_RequiresSpeed",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "CardioStage_Stages",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "CardioStage_State",
                table: "ExerciseProgressions");

            migrationBuilder.DropColumn(
                name: "CardioStage_WarmUp",
                table: "ExerciseProgressions");
        }
    }
}
