using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class fixRecipeLocalizedName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /* fix recipe localized field*/
            migrationBuilder.Sql(@"
                UPDATE Recipe
                SET LocalizedNameId = Id
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_LocalizedNameId",
                table: "Recipe",
                column: "LocalizedNameId");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipe_LocalizedField_Id",
                table: "Recipe");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
