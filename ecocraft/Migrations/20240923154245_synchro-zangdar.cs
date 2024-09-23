using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class synchrozangdar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SecretId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Pseudo = table.Column<string>(type: "TEXT", nullable: false),
                    SecretId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CraftingTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraftingTables_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemOrTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsTag = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinPrice = table.Column<float>(type: "REAL", nullable: false),
                    MaxPrice = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemOrTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemOrTags_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginModules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Percent = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginModules_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserServer",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserServer", x => new { x.UserId, x.ServerId });
                    table.ForeignKey(
                        name: "FK_UserServer_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserServer_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CalorieCost = table.Column<float>(type: "REAL", nullable: false),
                    Margin = table.Column<float>(type: "REAL", nullable: false),
                    TimeFee = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTagAssoc",
                columns: table => new
                {
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTagAssoc", x => new { x.TagId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_ItemTagAssoc_ItemOrTags_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTagAssoc_ItemOrTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<float>(type: "REAL", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPrices_ItemOrTags_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPrices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CraftingTablePluginModule",
                columns: table => new
                {
                    CraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTablePluginModule", x => new { x.CraftingTableId, x.PluginModuleId });
                    table.ForeignKey(
                        name: "FK_CraftingTablePluginModule_CraftingTables_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraftingTablePluginModule_PluginModules_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCraftingTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCraftingTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCraftingTables_CraftingTables_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTables_PluginModules_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTables_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FamilyName = table.Column<string>(type: "TEXT", nullable: false),
                    CraftMinutes = table.Column<float>(type: "REAL", nullable: false),
                    SkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SkillLevel = table.Column<long>(type: "INTEGER", nullable: false),
                    IsBlueprint = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Labor = table.Column<float>(type: "REAL", nullable: false),
                    CraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_CraftingTables_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipes_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipes_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    HasLavishTalent = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSkills_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Elements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<float>(type: "REAL", nullable: false),
                    IsDynamic = table.Column<bool>(type: "INTEGER", nullable: false),
                    SkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LavishTalent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Elements_ItemOrTags_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Elements_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Elements_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserElements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Share = table.Column<float>(type: "REAL", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserElements_Elements_ElementId",
                        column: x => x.ElementId,
                        principalTable: "Elements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserElements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTablePluginModule_PluginModuleId",
                table: "CraftingTablePluginModule",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTables_ServerId",
                table: "CraftingTables",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Elements_ItemOrTagId",
                table: "Elements",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Elements_RecipeId",
                table: "Elements",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Elements_SkillId",
                table: "Elements",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemOrTags_ServerId",
                table: "ItemOrTags",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagAssoc_ItemId",
                table: "ItemTagAssoc",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginModules_ServerId",
                table: "PluginModules",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CraftingTableId",
                table: "Recipes",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ServerId",
                table: "Recipes",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SkillId",
                table: "Recipes",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ServerId",
                table: "Skills",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_CraftingTableId",
                table: "UserCraftingTables",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_PluginModuleId",
                table: "UserCraftingTables",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_UserId",
                table: "UserCraftingTables",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElements_ElementId",
                table: "UserElements",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElements_UserId",
                table: "UserElements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrices_ItemOrTagId",
                table: "UserPrices",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrices_UserId",
                table: "UserPrices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServer_ServerId",
                table: "UserServer",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_ServerId",
                table: "UserSettings",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSkills_SkillId",
                table: "UserSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSkills_UserId",
                table: "UserSkills",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CraftingTablePluginModule");

            migrationBuilder.DropTable(
                name: "ItemTagAssoc");

            migrationBuilder.DropTable(
                name: "UserCraftingTables");

            migrationBuilder.DropTable(
                name: "UserElements");

            migrationBuilder.DropTable(
                name: "UserPrices");

            migrationBuilder.DropTable(
                name: "UserServer");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "UserSkills");

            migrationBuilder.DropTable(
                name: "PluginModules");

            migrationBuilder.DropTable(
                name: "Elements");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ItemOrTags");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "CraftingTables");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Servers");
        }
    }
}
