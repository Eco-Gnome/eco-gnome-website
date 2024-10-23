using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class margin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserMarginId",
                table: "UserPrice",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_UserMarginId",
                table: "UserPrice",
                column: "UserMarginId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice",
                column: "UserMarginId",
                principalTable: "UserMargin",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice");

            migrationBuilder.DropIndex(
                name: "IX_UserPrice_UserMarginId",
                table: "UserPrice");

            migrationBuilder.DropColumn(
                name: "UserMarginId",
                table: "UserPrice");
        }
    }
}
