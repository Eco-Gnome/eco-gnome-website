using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class AddItemFuelFoodHousing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "AcceptedFuelTags",
                table: "ItemOrTag",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodCalories",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodCarbs",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodFat",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodProtein",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodVitamins",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelCalories",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HousingBaseValue",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HousingDiminishingMultiplierAcrossFullProperty",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HousingDiminishingReturnMultiplier",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HousingRoomCategory",
                table: "ItemOrTag",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HousingTypeForRoomLimit",
                table: "ItemOrTag",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedFuelTags",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "FoodCalories",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "FoodCarbs",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "FoodFat",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "FoodProtein",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "FoodVitamins",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "FuelCalories",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "HousingBaseValue",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "HousingDiminishingMultiplierAcrossFullProperty",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "HousingDiminishingReturnMultiplier",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "HousingRoomCategory",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "HousingTypeForRoomLimit",
                table: "ItemOrTag");
        }
    }
}
