using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addDataContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTable_UserServer_UserServerId",
                table: "UserCraftingTable");

            migrationBuilder.DropForeignKey(
                name: "FK_UserElement_UserServer_UserServerId",
                table: "UserElement");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMargin_UserServer_UserServerId",
                table: "UserMargin");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPrice_UserServer_UserServerId",
                table: "UserPrice");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRecipe_UserServer_UserServerId",
                table: "UserRecipe");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSetting_UserServer_UserServerId",
                table: "UserSetting");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSkill_UserServer_UserServerId",
                table: "UserSkill");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTalent_UserServer_UserServerId",
                table: "UserTalent");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserTalent",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTalent_UserServerId",
                table: "UserTalent",
                newName: "IX_UserTalent_DataContextId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserSkill",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSkill_UserServerId",
                table: "UserSkill",
                newName: "IX_UserSkill_DataContextId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserSetting",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSetting_UserServerId",
                table: "UserSetting",
                newName: "IX_UserSetting_DataContextId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserRecipe",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserRecipe_UserServerId",
                table: "UserRecipe",
                newName: "IX_UserRecipe_DataContextId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserPrice",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserPrice_UserServerId",
                table: "UserPrice",
                newName: "IX_UserPrice_DataContextId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserMargin",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserMargin_UserServerId",
                table: "UserMargin",
                newName: "IX_UserMargin_DataContextId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserElement",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserElement_UserServerId",
                table: "UserElement",
                newName: "IX_UserElement_DataContextId");

            migrationBuilder.RenameColumn(
                name: "UserServerId",
                table: "UserCraftingTable",
                newName: "DataContextId");

            migrationBuilder.RenameIndex(
                name: "IX_UserCraftingTable_UserServerId",
                table: "UserCraftingTable",
                newName: "IX_UserCraftingTable_DataContextId");

            migrationBuilder.CreateTable(
                name: "DataContext",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataContext", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataContext_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataContext_UserServerId",
                table: "DataContext",
                column: "UserServerId");

            migrationBuilder.Sql(@"
                INSERT INTO DataContext (Id, UserServerId, Name, IsDefault)
                    SELECT Id, Id as UserServerId, 'Default Context', 1
                    FROM UserServer
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTable_DataContext_DataContextId",
                table: "UserCraftingTable",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserElement_DataContext_DataContextId",
                table: "UserElement",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMargin_DataContext_DataContextId",
                table: "UserMargin",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrice_DataContext_DataContextId",
                table: "UserPrice",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRecipe_DataContext_DataContextId",
                table: "UserRecipe",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSetting_DataContext_DataContextId",
                table: "UserSetting",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkill_DataContext_DataContextId",
                table: "UserSkill",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTalent_DataContext_DataContextId",
                table: "UserTalent",
                column: "DataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTable_DataContext_DataContextId",
                table: "UserCraftingTable");

            migrationBuilder.DropForeignKey(
                name: "FK_UserElement_DataContext_DataContextId",
                table: "UserElement");

            migrationBuilder.DropForeignKey(
                name: "FK_UserMargin_DataContext_DataContextId",
                table: "UserMargin");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPrice_DataContext_DataContextId",
                table: "UserPrice");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRecipe_DataContext_DataContextId",
                table: "UserRecipe");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSetting_DataContext_DataContextId",
                table: "UserSetting");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSkill_DataContext_DataContextId",
                table: "UserSkill");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTalent_DataContext_DataContextId",
                table: "UserTalent");

            migrationBuilder.DropTable(
                name: "DataContext");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserTalent",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTalent_DataContextId",
                table: "UserTalent",
                newName: "IX_UserTalent_UserServerId");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserSkill",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSkill_DataContextId",
                table: "UserSkill",
                newName: "IX_UserSkill_UserServerId");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserSetting",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSetting_DataContextId",
                table: "UserSetting",
                newName: "IX_UserSetting_UserServerId");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserRecipe",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserRecipe_DataContextId",
                table: "UserRecipe",
                newName: "IX_UserRecipe_UserServerId");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserPrice",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserPrice_DataContextId",
                table: "UserPrice",
                newName: "IX_UserPrice_UserServerId");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserMargin",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserMargin_DataContextId",
                table: "UserMargin",
                newName: "IX_UserMargin_UserServerId");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserElement",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserElement_DataContextId",
                table: "UserElement",
                newName: "IX_UserElement_UserServerId");

            migrationBuilder.RenameColumn(
                name: "DataContextId",
                table: "UserCraftingTable",
                newName: "UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserCraftingTable_DataContextId",
                table: "UserCraftingTable",
                newName: "IX_UserCraftingTable_UserServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTable_UserServer_UserServerId",
                table: "UserCraftingTable",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserElement_UserServer_UserServerId",
                table: "UserElement",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMargin_UserServer_UserServerId",
                table: "UserMargin",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrice_UserServer_UserServerId",
                table: "UserPrice",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRecipe_UserServer_UserServerId",
                table: "UserRecipe",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSetting_UserServer_UserServerId",
                table: "UserSetting",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkill_UserServer_UserServerId",
                table: "UserSkill",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTalent_UserServer_UserServerId",
                table: "UserTalent",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
