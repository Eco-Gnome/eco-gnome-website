using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class margins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice",
                column: "UserMarginId",
                principalTable: "UserMargin",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice",
                column: "UserMarginId",
                principalTable: "UserMargin",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
