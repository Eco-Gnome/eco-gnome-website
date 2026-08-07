using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class EcoV14ModuleSlotBonusSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PluginModule_Skill_SkillId",
                table: "PluginModule");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTable_PluginModule_PluginModuleId",
                table: "UserCraftingTable");

            migrationBuilder.DropIndex(
                name: "IX_UserCraftingTable_PluginModuleId",
                table: "UserCraftingTable");

            // Preserve the previously selected module: under v4 the single PluginModuleId FK becomes
            // one row of the UserCraftingTablePluginModule join table (installed module per slot).
            // The old "skilled" toggles already live in that join table and are kept as-is.
            migrationBuilder.Sql("""
                INSERT INTO "UserCraftingTablePluginModule" ("UserCraftingTableId", "PluginModuleId")
                SELECT "Id", "PluginModuleId"
                FROM "UserCraftingTable"
                WHERE "PluginModuleId" IS NOT NULL
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropColumn(
                name: "PluginModuleId",
                table: "UserCraftingTable");

            migrationBuilder.DropColumn(
                name: "Percent",
                table: "PluginModule");

            migrationBuilder.DropColumn(
                name: "PluginType",
                table: "PluginModule");

            migrationBuilder.RenameColumn(
                name: "SkillPercent",
                table: "PluginModule",
                newName: "MaterialTierBump");

            migrationBuilder.RenameColumn(
                name: "SkillId",
                table: "PluginModule",
                newName: "ModuleSlotId");

            migrationBuilder.RenameIndex(
                name: "IX_PluginModule_SkillId",
                table: "PluginModule",
                newName: "IX_PluginModule_ModuleSlotId");

            // The renamed columns held v3 data (a Skill id and a skill percentage) that has no
            // meaning under v4 — clear them so the ModuleSlot FK can be created. The next
            // Version-4 import repopulates slot, tier bump and bonuses for every module.
            migrationBuilder.Sql("""
                UPDATE "PluginModule" SET "ModuleSlotId" = NULL, "MaterialTierBump" = NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TalentId",
                table: "TalentBonus",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "Chance",
                table: "TalentBonus",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "ExcludedSkillTypes",
                table: "TalentBonus",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "Levels",
                table: "TalentBonus",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PluginModuleId",
                table: "TalentBonus",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "SkillTypes",
                table: "TalentBonus",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomMaterialTier",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RoomRequiresContainment",
                table: "ItemOrTag",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomVolume",
                table: "ItemOrTag",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomMaterialTier",
                table: "CraftingTable",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RoomRequiresContainment",
                table: "CraftingTable",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomVolume",
                table: "CraftingTable",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModuleSlot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LocalizedNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleSlot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleSlot_LocalizedField_LocalizedNameId",
                        column: x => x.LocalizedNameId,
                        principalTable: "LocalizedField",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModuleSlot_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CraftingTableModuleSlot",
                columns: table => new
                {
                    CraftingTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleSlotId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTableModuleSlot", x => new { x.CraftingTableId, x.ModuleSlotId });
                    table.ForeignKey(
                        name: "FK_CraftingTableModuleSlot_CraftingTable_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraftingTableModuleSlot_ModuleSlot_ModuleSlotId",
                        column: x => x.ModuleSlotId,
                        principalTable: "ModuleSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalentBonus_PluginModuleId",
                table: "TalentBonus",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTableModuleSlot_ModuleSlotId",
                table: "CraftingTableModuleSlot",
                column: "ModuleSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleSlot_LocalizedNameId",
                table: "ModuleSlot",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleSlot_ServerId",
                table: "ModuleSlot",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PluginModule_ModuleSlot_ModuleSlotId",
                table: "PluginModule",
                column: "ModuleSlotId",
                principalTable: "ModuleSlot",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TalentBonus_PluginModule_PluginModuleId",
                table: "TalentBonus",
                column: "PluginModuleId",
                principalTable: "PluginModule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PluginModule_ModuleSlot_ModuleSlotId",
                table: "PluginModule");

            migrationBuilder.DropForeignKey(
                name: "FK_TalentBonus_PluginModule_PluginModuleId",
                table: "TalentBonus");

            // Mirror of the data clearing in Up(): v4 data has no meaning under the v3 schema.
            // Module-owned bonus rows (TalentId NULL) must go before TalentId is made non-null
            // again (SET NOT NULL would fail, and Guid.Empty would violate FK_TalentBonus_Talent),
            // and the ModuleSlot ids must be cleared before the column is renamed back to SkillId
            // and re-pointed at the Skill table.
            migrationBuilder.Sql("""
                DELETE FROM "TalentBonus" WHERE "TalentId" IS NULL;
                UPDATE "PluginModule" SET "ModuleSlotId" = NULL, "MaterialTierBump" = NULL;
                """);

            migrationBuilder.DropTable(
                name: "CraftingTableModuleSlot");

            migrationBuilder.DropTable(
                name: "ModuleSlot");

            migrationBuilder.DropIndex(
                name: "IX_TalentBonus_PluginModuleId",
                table: "TalentBonus");

            migrationBuilder.DropColumn(
                name: "Chance",
                table: "TalentBonus");

            migrationBuilder.DropColumn(
                name: "ExcludedSkillTypes",
                table: "TalentBonus");

            migrationBuilder.DropColumn(
                name: "Levels",
                table: "TalentBonus");

            migrationBuilder.DropColumn(
                name: "PluginModuleId",
                table: "TalentBonus");

            migrationBuilder.DropColumn(
                name: "SkillTypes",
                table: "TalentBonus");

            migrationBuilder.DropColumn(
                name: "RoomMaterialTier",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "RoomRequiresContainment",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "RoomVolume",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "RoomMaterialTier",
                table: "CraftingTable");

            migrationBuilder.DropColumn(
                name: "RoomRequiresContainment",
                table: "CraftingTable");

            migrationBuilder.DropColumn(
                name: "RoomVolume",
                table: "CraftingTable");

            migrationBuilder.RenameColumn(
                name: "ModuleSlotId",
                table: "PluginModule",
                newName: "SkillId");

            migrationBuilder.RenameColumn(
                name: "MaterialTierBump",
                table: "PluginModule",
                newName: "SkillPercent");

            migrationBuilder.RenameIndex(
                name: "IX_PluginModule_ModuleSlotId",
                table: "PluginModule",
                newName: "IX_PluginModule_SkillId");

            migrationBuilder.AddColumn<Guid>(
                name: "PluginModuleId",
                table: "UserCraftingTable",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TalentId",
                table: "TalentBonus",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Percent",
                table: "PluginModule",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PluginType",
                table: "PluginModule",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_PluginModuleId",
                table: "UserCraftingTable",
                column: "PluginModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_PluginModule_Skill_SkillId",
                table: "PluginModule",
                column: "SkillId",
                principalTable: "Skill",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTable_PluginModule_PluginModuleId",
                table: "UserCraftingTable",
                column: "PluginModuleId",
                principalTable: "PluginModule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
