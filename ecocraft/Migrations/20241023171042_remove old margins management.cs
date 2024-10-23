using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class removeoldmarginsmanagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMargin_UserSetting_UserSettingId",
                table: "UserMargin");

            migrationBuilder.DropIndex(
                name: "IX_UserMargin_UserSettingId",
                table: "UserMargin");

            migrationBuilder.DropColumn(
                name: "MarginNames",
                table: "UserSetting");

            migrationBuilder.DropColumn(
                name: "MarginValues",
                table: "UserSetting");

            migrationBuilder.DropColumn(
                name: "UserSettingId",
                table: "UserMargin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarginNames",
                table: "UserSetting",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MarginValues",
                table: "UserSetting",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UserSettingId",
                table: "UserMargin",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMargin_UserSettingId",
                table: "UserMargin",
                column: "UserSettingId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMargin_UserSetting_UserSettingId",
                table: "UserMargin",
                column: "UserSettingId",
                principalTable: "UserSetting",
                principalColumn: "Id");
        }
    }
}
