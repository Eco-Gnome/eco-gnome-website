using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Server",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Server", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Pseudo = table.Column<string>(type: "TEXT", nullable: false),
                    SecretId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CraftingTable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraftingTable_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemOrTag",
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
                    table.PrimaryKey("PK_ItemOrTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemOrTag_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginModule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Percent = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginModule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginModule_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skill_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserServer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Pseudo = table.Column<string>(type: "TEXT", nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserServer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserServer_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserServer_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
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
                        name: "FK_ItemTagAssoc_ItemOrTag_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTagAssoc_ItemOrTag_TagId",
                        column: x => x.TagId,
                        principalTable: "ItemOrTag",
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
                        name: "FK_CraftingTablePluginModule_CraftingTable_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraftingTablePluginModule_PluginModule_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipe",
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
                    table.PrimaryKey("PK_Recipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipe_CraftingTable_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipe_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipe_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserCraftingTable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCraftingTable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCraftingTable_CraftingTable_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTable_PluginModule_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTable_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPrice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<float>(type: "REAL", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPrice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPrice_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPrice_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSetting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CalorieCost = table.Column<float>(type: "REAL", nullable: false),
                    Margin = table.Column<float>(type: "REAL", nullable: false),
                    TimeFee = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSetting_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSkill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    HasLavishTalent = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSkill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSkill_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSkill_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Element",
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
                    table.PrimaryKey("PK_Element", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Element_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Element_Recipe_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Element_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserElement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Share = table.Column<float>(type: "REAL", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserElement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserElement_Element_ElementId",
                        column: x => x.ElementId,
                        principalTable: "Element",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserElement_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTable_ServerId",
                table: "CraftingTable",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTablePluginModule_PluginModuleId",
                table: "CraftingTablePluginModule",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_ItemOrTagId",
                table: "Element",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_RecipeId",
                table: "Element",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_SkillId",
                table: "Element",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemOrTag_ServerId",
                table: "ItemOrTag",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagAssoc_ItemId",
                table: "ItemTagAssoc",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginModule_ServerId",
                table: "PluginModule",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_CraftingTableId",
                table: "Recipe",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_ServerId",
                table: "Recipe",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_SkillId",
                table: "Recipe",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skill_ServerId",
                table: "Skill",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_CraftingTableId",
                table: "UserCraftingTable",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_PluginModuleId",
                table: "UserCraftingTable",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_UserServerId",
                table: "UserCraftingTable",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElement_ElementId",
                table: "UserElement",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElement_UserServerId",
                table: "UserElement",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_ItemOrTagId",
                table: "UserPrice",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_UserServerId",
                table: "UserPrice",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServer_ServerId",
                table: "UserServer",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServer_UserId",
                table: "UserServer",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSetting_UserServerId",
                table: "UserSetting",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSkill_SkillId",
                table: "UserSkill",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSkill_UserServerId",
                table: "UserSkill",
                column: "UserServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CraftingTablePluginModule");

            migrationBuilder.DropTable(
                name: "ItemTagAssoc");

            migrationBuilder.DropTable(
                name: "UserCraftingTable");

            migrationBuilder.DropTable(
                name: "UserElement");

            migrationBuilder.DropTable(
                name: "UserPrice");

            migrationBuilder.DropTable(
                name: "UserSetting");

            migrationBuilder.DropTable(
                name: "UserSkill");

            migrationBuilder.DropTable(
                name: "PluginModule");

            migrationBuilder.DropTable(
                name: "Element");

            migrationBuilder.DropTable(
                name: "UserServer");

            migrationBuilder.DropTable(
                name: "ItemOrTag");

            migrationBuilder.DropTable(
                name: "Recipe");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "CraftingTable");

            migrationBuilder.DropTable(
                name: "Skill");

            migrationBuilder.DropTable(
                name: "Server");
        }
    }
}
