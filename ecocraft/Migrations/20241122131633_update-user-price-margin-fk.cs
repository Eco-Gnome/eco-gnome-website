using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class updateUserPriceMarginFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserMarginId",
                table: "UserPrice",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice",
                column: "UserMarginId",
                principalTable: "UserMargin",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserMarginId",
                table: "UserPrice",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrice_UserMargin_UserMarginId",
                table: "UserPrice",
                column: "UserMarginId",
                principalTable: "UserMargin",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
