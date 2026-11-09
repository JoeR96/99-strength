using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace A2S.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CutFriendly = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ingredients = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Steps = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    Tags = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Items = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    SourceMealIds = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingLists", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meals_UserId_ExternalKey",
                table: "Meals",
                columns: new[] { "UserId", "ExternalKey" },
                unique: true,
                filter: "\"ExternalKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_UserId_Type_Title",
                table: "Meals",
                columns: new[] { "UserId", "Type", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_UserId_WeekStart",
                table: "ShoppingLists",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropTable(
                name: "ShoppingLists");
        }
    }
}
