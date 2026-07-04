using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260704000000_AddCraftTimeLayerOverride")]
    public partial class AddCraftTimeLayerOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasLayerModifier",
                table: "DynamicValue",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CraftMinutesOverride",
                table: "UserRecipe",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasLayerModifier",
                table: "DynamicValue");

            migrationBuilder.DropColumn(
                name: "CraftMinutesOverride",
                table: "UserRecipe");
        }
    }
}
