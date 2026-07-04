using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260511000000_RenameCraftMinuteFeeToTotalCraftMinuteFee")]
    public partial class RenameCraftMinuteFeeToTotalCraftMinuteFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CraftMinuteFee",
                table: "UserCraftingTable",
                newName: "TotalCraftMinuteFee");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalCraftMinuteFee",
                table: "UserCraftingTable",
                newName: "CraftMinuteFee");
        }
    }
}
