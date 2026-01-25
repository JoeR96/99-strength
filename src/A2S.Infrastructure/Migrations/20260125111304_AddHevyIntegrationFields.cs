using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHevyIntegrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HevyRoutineFolderId",
                table: "Workouts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "HevySyncedRoutines",
                table: "Workouts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "HevyExerciseTemplateId",
                table: "Exercises",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HevyRoutineFolderId",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "HevySyncedRoutines",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "HevyExerciseTemplateId",
                table: "Exercises");
        }
    }
}
