using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class improvePluginModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PluginType",
                table: "PluginModule",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SkillId",
                table: "PluginModule",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SkillPercent",
                table: "PluginModule",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserCraftingTablePluginModule",
                columns: table => new
                {
                    UserCraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCraftingTablePluginModule", x => new { x.UserCraftingTableId, x.PluginModuleId });
                    table.ForeignKey(
                        name: "FK_UserCraftingTablePluginModule_PluginModule_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTablePluginModule_UserCraftingTable_UserCraftingTableId",
                        column: x => x.UserCraftingTableId,
                        principalTable: "UserCraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PluginModule_SkillId",
                table: "PluginModule",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTablePluginModule_PluginModuleId",
                table: "UserCraftingTablePluginModule",
                column: "PluginModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_PluginModule_Skill_SkillId",
                table: "PluginModule",
                column: "SkillId",
                principalTable: "Skill",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PluginModule_Skill_SkillId",
                table: "PluginModule");

            migrationBuilder.DropTable(
                name: "UserCraftingTablePluginModule");

            migrationBuilder.DropIndex(
                name: "IX_PluginModule_SkillId",
                table: "PluginModule");

            migrationBuilder.DropColumn(
                name: "PluginType",
                table: "PluginModule");

            migrationBuilder.DropColumn(
                name: "SkillId",
                table: "PluginModule");

            migrationBuilder.DropColumn(
                name: "SkillPercent",
                table: "PluginModule");
        }
    }
}
