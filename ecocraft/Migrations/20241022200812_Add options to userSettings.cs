using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class AddoptionstouserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisplayNonSkilledRecipes",
                table: "UserSetting",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnlyLevelAccessibleRecipes",
                table: "UserSetting",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayNonSkilledRecipes",
                table: "UserSetting");

            migrationBuilder.DropColumn(
                name: "OnlyLevelAccessibleRecipes",
                table: "UserSetting");
        }
    }
}
