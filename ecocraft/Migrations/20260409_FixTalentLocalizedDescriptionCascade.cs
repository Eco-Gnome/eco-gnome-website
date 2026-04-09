using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class FixTalentLocalizedDescriptionCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Talent_LocalizedField_LocalizedDescriptionId",
                table: "Talent");

            migrationBuilder.AddForeignKey(
                name: "FK_Talent_LocalizedField_LocalizedDescriptionId",
                table: "Talent",
                column: "LocalizedDescriptionId",
                principalTable: "LocalizedField",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Talent_LocalizedField_LocalizedDescriptionId",
                table: "Talent");

            migrationBuilder.AddForeignKey(
                name: "FK_Talent_LocalizedField_LocalizedDescriptionId",
                table: "Talent",
                column: "LocalizedDescriptionId",
                principalTable: "LocalizedField",
                principalColumn: "Id");
        }
    }
}
