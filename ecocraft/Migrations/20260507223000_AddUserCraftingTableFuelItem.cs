using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260507223000_AddUserCraftingTableFuelItem")]
    public partial class AddUserCraftingTableFuelItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FuelItemId",
                table: "UserCraftingTable",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_FuelItemId",
                table: "UserCraftingTable",
                column: "FuelItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTable_ItemOrTag_FuelItemId",
                table: "UserCraftingTable",
                column: "FuelItemId",
                principalTable: "ItemOrTag",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTable_ItemOrTag_FuelItemId",
                table: "UserCraftingTable");

            migrationBuilder.DropIndex(
                name: "IX_UserCraftingTable_FuelItemId",
                table: "UserCraftingTable");

            migrationBuilder.DropColumn(
                name: "FuelItemId",
                table: "UserCraftingTable");
        }
    }
}
