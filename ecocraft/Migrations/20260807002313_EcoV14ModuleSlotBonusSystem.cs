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

            // ---- v3 -> v4 data preservation ----
            // A v4 re-import refreshes everything below by name, but servers that never re-import
            // (or run an Eco version whose mod still exports v3 files) must keep working modules.

            // 1. Convert the old flat percentages into v4 bonus rows while Percent/PluginType/
            //    SkillId/SkillPercent still exist. Old semantics: Percent is a cost multiplier
            //    (0.9 = -10%) applied to resources (PluginType 1/Resource, 3 = both) and/or craft
            //    time (2/Speed, 3); SkillPercent replaces Percent when the recipe's skill matches
            //    the module's linked skill — expressed here as an ExcludedSkillTypes/SkillTypes pair.
            migrationBuilder.Sql("""
                INSERT INTO "TalentBonus" ("Id", "TalentId", "PluginModuleId", "Action", "EffectType", "Value", "ExcludedSkillTypes")
                SELECT gen_random_uuid(), NULL, pm."Id", act.a, 0, pm."Percent",
                       CASE WHEN s."Name" IS NOT NULL AND pm."SkillPercent" IS NOT NULL THEN ARRAY[s."Name"] END
                FROM "PluginModule" pm
                LEFT JOIN "Skill" s ON s."Id" = pm."SkillId"
                CROSS JOIN (VALUES (0), (2)) AS act(a)
                WHERE pm."Percent" NOT IN (0, 1)
                  AND ((act.a = 0 AND pm."PluginType" IN (1, 3)) OR (act.a = 2 AND pm."PluginType" IN (2, 3)));

                INSERT INTO "TalentBonus" ("Id", "TalentId", "PluginModuleId", "Action", "EffectType", "Value", "SkillTypes")
                SELECT gen_random_uuid(), NULL, pm."Id", act.a, 0, pm."SkillPercent", ARRAY[s."Name"]
                FROM "PluginModule" pm
                JOIN "Skill" s ON s."Id" = pm."SkillId"
                CROSS JOIN (VALUES (0), (2)) AS act(a)
                WHERE pm."SkillPercent" IS NOT NULL AND pm."SkillPercent" NOT IN (0, 1)
                  AND ((act.a = 0 AND pm."PluginType" IN (1, 3)) OR (act.a = 2 AND pm."PluginType" IN (2, 3)));
                """);

            // 2. Create the four v14 slots (with the game's localized labels) on every server that
            //    owns modules. Names match the v4 export so a later import reconciles instead of
            //    duplicating.
            migrationBuilder.Sql("""
                WITH servers AS (
                    SELECT DISTINCT "ServerId" AS id FROM "PluginModule"
                ),
                slots(name, sort, en_us, fr, es, de, ko, pt_br, zh_hans, ru, it, pt_pt, hu, ja, nn, pl, nl, ro, da, cs, sv, uk, el, ar_sa, vi, tr) AS (VALUES
                    ('BasicModule', 0, 'Basic', 'Basique', 'Básico', 'Grundlegend', '기본', 'Básico', '基础的', 'Базовый', 'Base', 'Básico', 'Alapvető', '基本', 'Grunnleggende', 'Podstawowe', 'Basis', 'Bazic', 'Grundlæggende', 'Základní', 'Grundläggande', 'Основний', 'Βασικά', 'الأساسية', 'Cơ bản', 'Temel'),
                    ('AdvancedModule', 1, 'Advanced', 'Avancé', 'Avanzado', 'Fortgeschritten', '고급', 'Avançado', '进阶', 'Продвинутый', 'Avanzato', 'Avançado', 'Haladó', '上級', 'Avansert', 'Zaawansowane', 'Geavanceerd', 'Nivel avansat', 'Avanceret', 'Pokročilé', 'Avancerat', 'Для досвідчених гравців', 'Για προχωρημένους', 'متقدم', 'Nâng cao', 'Gelişmiş'),
                    ('ModernModule', 2, 'Modern', 'Moderne', 'Moderno', 'Modern', '현대', 'Moderno', '现代', 'Современный', 'Moderno', 'Moderno', 'Modern', '現代', 'Moderne', 'Nowoczesna', 'Modern', 'Modern', 'Moderne', 'Moderní', 'Modernt', 'Сучасна', 'Σύγχρονο', 'الحديثة', 'Hiện đại', 'Modern'),
                    ('SpecialtyModule', 3, 'Specialty', 'Spécialité', 'Especialidad', 'Spezialisierung', '특기', 'Especialidade', '专业', 'Специальность', 'Specialità', 'Especialidade', 'Különlegesség', '専門分野', 'Spesialitet', 'Specjalność', 'Specialiteit', 'Specialitate', 'Specialitet', 'Specializace', 'Specialitet', 'Спеціальність', 'Ειδικότητα', 'التخصص', 'Điểm đặc biệt', 'Uzmanlık')
                ),
                lf AS (
                    INSERT INTO "LocalizedField" ("Id", "ServerId", "en_US", "fr", "es", "de", "ko", "pt_BR", "zh_Hans", "ru", "it", "pt_PT", "hu", "ja", "nn", "pl", "nl", "ro", "da", "cs", "sv", "uk", "el", "ar_sa", "vi", "tr")
                    SELECT gen_random_uuid(), servers.id, s.en_us, s.fr, s.es, s.de, s.ko, s.pt_br, s.zh_hans, s.ru, s.it, s.pt_pt, s.hu, s.ja, s.nn, s.pl, s.nl, s.ro, s.da, s.cs, s.sv, s.uk, s.el, s.ar_sa, s.vi, s.tr
                    FROM servers CROSS JOIN slots s
                    RETURNING "Id", "ServerId", "en_US"
                )
                INSERT INTO "ModuleSlot" ("Id", "Name", "LocalizedNameId", "SortOrder", "ServerId")
                SELECT gen_random_uuid(), s.name, lf."Id", s.sort, lf."ServerId"
                FROM lf
                JOIN slots s ON s.en_us = lf."en_US";
                """);

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

            // 3. The renamed columns held v3 data (a Skill id and a skill percentage) already
            //    converted to bonuses above — clear them, then slot each module by its item name
            //    (mirroring the v14 layout: Basic/Advanced/Modern families, everything else is a
            //    specialty module) and let tables expose exactly the slots their accepted modules
            //    occupy.
            migrationBuilder.Sql("""
                UPDATE "PluginModule" SET "ModuleSlotId" = NULL, "MaterialTierBump" = NULL;

                UPDATE "PluginModule" pm
                SET "ModuleSlotId" = ms."Id"
                FROM "ModuleSlot" ms
                WHERE ms."ServerId" = pm."ServerId"
                  AND ms."Name" = CASE
                      WHEN pm."Name" LIKE 'BasicUpgrade%' THEN 'BasicModule'
                      WHEN pm."Name" LIKE 'AdvancedUpgrade%' THEN 'AdvancedModule'
                      WHEN pm."Name" LIKE 'ModernUpgrade%' THEN 'ModernModule'
                      ELSE 'SpecialtyModule'
                  END;

                INSERT INTO "CraftingTableModuleSlot" ("CraftingTableId", "ModuleSlotId")
                SELECT DISTINCT ctpm."CraftingTableId", pm."ModuleSlotId"
                FROM "CraftingTablePluginModule" ctpm
                JOIN "PluginModule" pm ON pm."Id" = ctpm."PluginModuleId"
                WHERE pm."ModuleSlotId" IS NOT NULL;
                """);

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
            // and re-pointed at the Skill table. The slots' localized labels go with the slots.
            migrationBuilder.Sql("""
                DELETE FROM "TalentBonus" WHERE "TalentId" IS NULL;
                UPDATE "PluginModule" SET "ModuleSlotId" = NULL, "MaterialTierBump" = NULL;
                DELETE FROM "LocalizedField" lf USING "ModuleSlot" ms WHERE ms."LocalizedNameId" = lf."Id";
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
