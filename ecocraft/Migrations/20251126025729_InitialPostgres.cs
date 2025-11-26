using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Server",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EcoServerId = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    HasVideoUploader = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastDataUploadTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JoinCode = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Server", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pseudo = table.Column<string>(type: "text", nullable: false),
                    CreationDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuperAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CanUploadMod = table.Column<bool>(type: "boolean", nullable: false),
                    ShowHelp = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DynamicValue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseValue = table.Column<decimal>(type: "numeric", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicValue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicValue_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalizedField",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    en_US = table.Column<string>(type: "text", nullable: false),
                    fr = table.Column<string>(type: "text", nullable: false),
                    es = table.Column<string>(type: "text", nullable: false),
                    de = table.Column<string>(type: "text", nullable: false),
                    ko = table.Column<string>(type: "text", nullable: false),
                    pt_BR = table.Column<string>(type: "text", nullable: false),
                    zh_Hans = table.Column<string>(type: "text", nullable: false),
                    ru = table.Column<string>(type: "text", nullable: false),
                    it = table.Column<string>(type: "text", nullable: false),
                    pt_PT = table.Column<string>(type: "text", nullable: false),
                    hu = table.Column<string>(type: "text", nullable: false),
                    ja = table.Column<string>(type: "text", nullable: false),
                    nn = table.Column<string>(type: "text", nullable: false),
                    pl = table.Column<string>(type: "text", nullable: false),
                    nl = table.Column<string>(type: "text", nullable: false),
                    ro = table.Column<string>(type: "text", nullable: false),
                    da = table.Column<string>(type: "text", nullable: false),
                    cs = table.Column<string>(type: "text", nullable: false),
                    sv = table.Column<string>(type: "text", nullable: false),
                    uk = table.Column<string>(type: "text", nullable: false),
                    el = table.Column<string>(type: "text", nullable: false),
                    ar_sa = table.Column<string>(type: "text", nullable: false),
                    vi = table.Column<string>(type: "text", nullable: false),
                    tr = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizedField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalizedField_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModUploadHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FileHash = table.Column<string>(type: "text", nullable: false),
                    IconsCount = table.Column<int>(type: "integer", nullable: false),
                    UploadDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModUploadHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModUploadHistory_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModUploadHistory_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserServer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pseudo = table.Column<string>(type: "text", nullable: true),
                    EcoUserId = table.Column<string>(type: "text", nullable: true),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "CraftingTable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LocalizedNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraftingTable_LocalizedField_LocalizedNameId",
                        column: x => x.LocalizedNameId,
                        principalTable: "LocalizedField",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LocalizedNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsTag = table.Column<bool>(type: "boolean", nullable: false),
                    MinPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    DefaultPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemOrTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemOrTag_LocalizedField_LocalizedNameId",
                        column: x => x.LocalizedNameId,
                        principalTable: "LocalizedField",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemOrTag_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LocalizedNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Profession = table.Column<string>(type: "text", nullable: true),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false),
                    LaborReducePercent = table.Column<decimal[]>(type: "numeric[]", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skill_LocalizedField_LocalizedNameId",
                        column: x => x.LocalizedNameId,
                        principalTable: "LocalizedField",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Skill_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataContext",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsShoppingList = table.Column<bool>(type: "boolean", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ItemTagAssoc",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTagAssoc", x => new { x.ItemId, x.TagId });
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
                name: "PluginModule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LocalizedNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    PluginType = table.Column<int>(type: "integer", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric", nullable: false),
                    SkillPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginModule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginModule_LocalizedField_LocalizedNameId",
                        column: x => x.LocalizedNameId,
                        principalTable: "LocalizedField",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PluginModule_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PluginModule_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LocalizedNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    FamilyName = table.Column<string>(type: "text", nullable: false),
                    CraftMinutesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    SkillLevel = table.Column<long>(type: "bigint", nullable: false),
                    IsBlueprint = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LaborId = table.Column<Guid>(type: "uuid", nullable: false),
                    CraftingTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false)
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
                        name: "FK_Recipe_DynamicValue_CraftMinutesId",
                        column: x => x.CraftMinutesId,
                        principalTable: "DynamicValue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipe_DynamicValue_LaborId",
                        column: x => x.LaborId,
                        principalTable: "DynamicValue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipe_LocalizedField_LocalizedNameId",
                        column: x => x.LocalizedNameId,
                        principalTable: "LocalizedField",
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
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Talent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LocalizedNameId = table.Column<Guid>(type: "uuid", nullable: true),
                    TalentGroupName = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Talent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Talent_LocalizedField_LocalizedNameId",
                        column: x => x.LocalizedNameId,
                        principalTable: "LocalizedField",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Talent_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMargin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Margin = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMargin", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMargin_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSetting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarginType = table.Column<int>(type: "integer", nullable: false),
                    CalorieCost = table.Column<decimal>(type: "numeric", nullable: false),
                    DisplayNonSkilledRecipes = table.Column<bool>(type: "boolean", nullable: false),
                    OnlyLevelAccessibleRecipes = table.Column<bool>(type: "boolean", nullable: false),
                    ApplyMarginBetweenSkills = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSetting_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSkill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSkill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSkill_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSkill_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CraftingTablePluginModule",
                columns: table => new
                {
                    CraftingTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "UserCraftingTable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    CraftingTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CraftMinuteFee = table.Column<decimal>(type: "numeric", nullable: false)
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
                        name: "FK_UserCraftingTable_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTable_PluginModule_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Element",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    QuantityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultIsReintegrated = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultShare = table.Column<decimal>(type: "numeric", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Element", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Element_DynamicValue_QuantityId",
                        column: x => x.QuantityId,
                        principalTable: "DynamicValue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "UserRecipe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundFactor = table.Column<int>(type: "integer", nullable: false),
                    LockShare = table.Column<bool>(type: "boolean", nullable: false),
                    ParentUserRecipeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRecipe_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRecipe_Recipe_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRecipe_UserRecipe_ParentUserRecipeId",
                        column: x => x.ParentUserRecipeId,
                        principalTable: "UserRecipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Modifier",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DynamicType = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    DynamicValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modifier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Modifier_DynamicValue_DynamicValueId",
                        column: x => x.DynamicValueId,
                        principalTable: "DynamicValue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Modifier_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Modifier_Talent_TalentId",
                        column: x => x.TalentId,
                        principalTable: "Talent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTalent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTalent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTalent_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTalent_Talent_TalentId",
                        column: x => x.TalentId,
                        principalTable: "Talent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCraftingTablePluginModule",
                columns: table => new
                {
                    UserCraftingTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "uuid", nullable: false)
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
                        name: "FK_UserCraftingTablePluginModule_UserCraftingTable_UserCraftin~",
                        column: x => x.UserCraftingTableId,
                        principalTable: "UserCraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserElement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    IsMarginPrice = table.Column<bool>(type: "boolean", nullable: false),
                    Share = table.Column<decimal>(type: "numeric", nullable: false),
                    IsReintegrated = table.Column<bool>(type: "boolean", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserRecipeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserElement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserElement_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserElement_Element_ElementId",
                        column: x => x.ElementId,
                        principalTable: "Element",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserElement_UserRecipe_UserRecipeId",
                        column: x => x.UserRecipeId,
                        principalTable: "UserRecipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPrice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    MarginPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    PrimaryUserElementId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryUserPriceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverrideIsBought = table.Column<bool>(type: "boolean", nullable: false),
                    UserMarginId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPrice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPrice_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPrice_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPrice_UserElement_PrimaryUserElementId",
                        column: x => x.PrimaryUserElementId,
                        principalTable: "UserElement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPrice_UserMargin_UserMarginId",
                        column: x => x.UserMarginId,
                        principalTable: "UserMargin",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserPrice_UserPrice_PrimaryUserPriceId",
                        column: x => x.PrimaryUserPriceId,
                        principalTable: "UserPrice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTable_LocalizedNameId",
                table: "CraftingTable",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTable_ServerId",
                table: "CraftingTable",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTablePluginModule_PluginModuleId",
                table: "CraftingTablePluginModule",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_DataContext_UserServerId",
                table: "DataContext",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicValue_ServerId",
                table: "DynamicValue",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_ItemOrTagId",
                table: "Element",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_QuantityId",
                table: "Element",
                column: "QuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_RecipeId",
                table: "Element",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_SkillId",
                table: "Element",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemOrTag_LocalizedNameId",
                table: "ItemOrTag",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemOrTag_ServerId",
                table: "ItemOrTag",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagAssoc_TagId",
                table: "ItemTagAssoc",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalizedField_ServerId",
                table: "LocalizedField",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifier_DynamicValueId",
                table: "Modifier",
                column: "DynamicValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifier_SkillId",
                table: "Modifier",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifier_TalentId",
                table: "Modifier",
                column: "TalentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModUploadHistory_ServerId",
                table: "ModUploadHistory",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ModUploadHistory_UserId",
                table: "ModUploadHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginModule_LocalizedNameId",
                table: "PluginModule",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginModule_ServerId",
                table: "PluginModule",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginModule_SkillId",
                table: "PluginModule",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_CraftingTableId",
                table: "Recipe",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_CraftMinutesId",
                table: "Recipe",
                column: "CraftMinutesId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_LaborId",
                table: "Recipe",
                column: "LaborId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_LocalizedNameId",
                table: "Recipe",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_ServerId",
                table: "Recipe",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_SkillId",
                table: "Recipe",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skill_LocalizedNameId",
                table: "Skill",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_Skill_ServerId",
                table: "Skill",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Talent_LocalizedNameId",
                table: "Talent",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_Talent_SkillId",
                table: "Talent",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_CraftingTableId",
                table: "UserCraftingTable",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_DataContextId",
                table: "UserCraftingTable",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTable_PluginModuleId",
                table: "UserCraftingTable",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTablePluginModule_PluginModuleId",
                table: "UserCraftingTablePluginModule",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElement_DataContextId",
                table: "UserElement",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElement_ElementId",
                table: "UserElement",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElement_UserRecipeId",
                table: "UserElement",
                column: "UserRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMargin_DataContextId",
                table: "UserMargin",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_DataContextId",
                table: "UserPrice",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_ItemOrTagId",
                table: "UserPrice",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_PrimaryUserElementId",
                table: "UserPrice",
                column: "PrimaryUserElementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_PrimaryUserPriceId",
                table: "UserPrice",
                column: "PrimaryUserPriceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrice_UserMarginId",
                table: "UserPrice",
                column: "UserMarginId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecipe_DataContextId",
                table: "UserRecipe",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecipe_ParentUserRecipeId",
                table: "UserRecipe",
                column: "ParentUserRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecipe_RecipeId",
                table: "UserRecipe",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServer_ServerId",
                table: "UserServer",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServer_UserId",
                table: "UserServer",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSetting_DataContextId",
                table: "UserSetting",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSkill_DataContextId",
                table: "UserSkill",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSkill_SkillId",
                table: "UserSkill",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTalent_DataContextId",
                table: "UserTalent",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTalent_TalentId",
                table: "UserTalent",
                column: "TalentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CraftingTablePluginModule");

            migrationBuilder.DropTable(
                name: "ItemTagAssoc");

            migrationBuilder.DropTable(
                name: "Modifier");

            migrationBuilder.DropTable(
                name: "ModUploadHistory");

            migrationBuilder.DropTable(
                name: "UserCraftingTablePluginModule");

            migrationBuilder.DropTable(
                name: "UserPrice");

            migrationBuilder.DropTable(
                name: "UserSetting");

            migrationBuilder.DropTable(
                name: "UserSkill");

            migrationBuilder.DropTable(
                name: "UserTalent");

            migrationBuilder.DropTable(
                name: "UserCraftingTable");

            migrationBuilder.DropTable(
                name: "UserElement");

            migrationBuilder.DropTable(
                name: "UserMargin");

            migrationBuilder.DropTable(
                name: "Talent");

            migrationBuilder.DropTable(
                name: "PluginModule");

            migrationBuilder.DropTable(
                name: "Element");

            migrationBuilder.DropTable(
                name: "UserRecipe");

            migrationBuilder.DropTable(
                name: "ItemOrTag");

            migrationBuilder.DropTable(
                name: "DataContext");

            migrationBuilder.DropTable(
                name: "Recipe");

            migrationBuilder.DropTable(
                name: "UserServer");

            migrationBuilder.DropTable(
                name: "CraftingTable");

            migrationBuilder.DropTable(
                name: "DynamicValue");

            migrationBuilder.DropTable(
                name: "Skill");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "LocalizedField");

            migrationBuilder.DropTable(
                name: "Server");
        }
    }
}
