using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addserver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTables_Users_UserId",
                table: "UserCraftingTables");

            migrationBuilder.DropForeignKey(
                name: "FK_UserElements_Users_UserId",
                table: "UserElements");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPrices_Users_UserId",
                table: "UserPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSettings_Servers_ServerId",
                table: "UserSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSettings_Users_UserId",
                table: "UserSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSkills_Users_UserId",
                table: "UserSkills");

            migrationBuilder.DropIndex(
                name: "IX_UserSettings_ServerId",
                table: "UserSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserServer",
                table: "UserServer");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "SecretId",
                table: "Servers");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserSkills",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSkills_UserId",
                table: "UserSkills",
                newName: "IX_UserSkills_UserServerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserSettings",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                newName: "IX_UserSettings_UserServerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserPrices",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserPrices_UserId",
                table: "UserPrices",
                newName: "IX_UserPrices_UserServerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserElements",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserElements_UserId",
                table: "UserElements",
                newName: "IX_UserElements_UserServerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserCraftingTables",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserCraftingTables_UserId",
                table: "UserCraftingTables",
                newName: "IX_UserCraftingTables_UserServerId");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "UserServer",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "UserServer",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Pseudo",
                table: "UserServer",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserServer",
                table: "UserServer",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserServer_UserId",
                table: "UserServer",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTables_UserServer_UserServerId",
                table: "UserCraftingTables",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserElements_UserServer_UserServerId",
                table: "UserElements",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrices_UserServer_UserServerId",
                table: "UserPrices",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSettings_UserServer_UserServerId",
                table: "UserSettings",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkills_UserServer_UserServerId",
                table: "UserSkills",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTables_UserServer_UserServerId",
                table: "UserCraftingTables");

            migrationBuilder.DropForeignKey(
                name: "FK_UserElements_UserServer_UserServerId",
                table: "UserElements");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPrices_UserServer_UserServerId",
                table: "UserPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSettings_UserServer_UserServerId",
                table: "UserSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSkills_UserServer_UserServerId",
                table: "UserSkills");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserServer",
                table: "UserServer");

            migrationBuilder.DropIndex(
                name: "IX_UserServer_UserId",
                table: "UserServer");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserServer");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "UserServer");

            migrationBuilder.DropColumn(
                name: "Pseudo",
                table: "UserServer");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserSkills",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSkills_UserServerId",
                table: "UserSkills",
                newName: "IX_UserSkills_UserId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserSettings",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSettings_UserServerId",
                table: "UserSettings",
                newName: "IX_UserSettings_UserId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserPrices",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserPrices_UserServerId",
                table: "UserPrices",
                newName: "IX_UserPrices_UserId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserElements",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserElements_UserServerId",
                table: "UserElements",
                newName: "IX_UserElements_UserId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserCraftingTables",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserCraftingTables_UserServerId",
                table: "UserCraftingTables",
                newName: "IX_UserCraftingTables_UserId");

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "UserSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SecretId",
                table: "Servers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserServer",
                table: "UserServer",
                columns: new[] { "UserId", "ServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_ServerId",
                table: "UserSettings",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTables_Users_UserId",
                table: "UserCraftingTables",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserElements_Users_UserId",
                table: "UserElements",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrices_Users_UserId",
                table: "UserPrices",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSettings_Servers_ServerId",
                table: "UserSettings",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSettings_Users_UserId",
                table: "UserSettings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkills_Users_UserId",
                table: "UserSkills",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
