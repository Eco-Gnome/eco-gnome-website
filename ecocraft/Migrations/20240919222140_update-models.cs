using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class updatemodels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTables_Upgrades_UpgradeId",
                table: "UserCraftingTables");

            migrationBuilder.DropTable(
                name: "CraftingTableSkills");

            migrationBuilder.DropTable(
                name: "CraftingTableUpgrades");

            migrationBuilder.DropTable(
                name: "RecipeMaterials");

            migrationBuilder.DropTable(
                name: "RecipeOutputs");

            migrationBuilder.DropTable(
                name: "UserInputPrices");

            migrationBuilder.DropTable(
                name: "Upgrades");

            migrationBuilder.DropIndex(
                name: "IX_UserCraftingTables_UpgradeId",
                table: "UserCraftingTables");

            migrationBuilder.DropColumn(
                name: "CalorieCost",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpgradeId",
                table: "UserCraftingTables");

            migrationBuilder.RenameColumn(
                name: "ProfitMargin",
                table: "Users",
                newName: "SecretId");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Users",
                newName: "Pseudo");

            migrationBuilder.RenameColumn(
                name: "MinimumSkillLevel",
                table: "Recipes",
                newName: "RequiredSkillLevel");

            migrationBuilder.RenameColumn(
                name: "CaloriesRequired",
                table: "Recipes",
                newName: "IsDefault");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "UserSkills",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<Guid>(
                name: "SkillId",
                table: "UserSkills",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserSkills",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<bool>(
                name: "HasLavishTalent",
                table: "UserSkills",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "UserSkills",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Users",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "UserCraftingTables",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<Guid>(
                name: "CraftingTableId",
                table: "UserCraftingTables",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserCraftingTables",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<Guid>(
                name: "PluginModuleId",
                table: "UserCraftingTables",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "UserCraftingTables",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Skills",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "Skills",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "SkillId",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<Guid>(
                name: "CraftingTableId",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<float>(
                name: "CraftMinutes",
                table: "Recipes",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "FamilyName",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsBlueprint",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "Labor",
                table: "Recipes",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "Recipes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "CraftingTables",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "CraftingTables",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
                name: "ItemOrTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MinPrice = table.Column<float>(type: "REAL", nullable: false),
                    MaxPrice = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Tag_ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemOrTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemOrTags_ItemOrTags_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ItemOrTags_ItemOrTags_Tag_ItemOrTagId",
                        column: x => x.Tag_ItemOrTagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id");
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
                name: "UserServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserServers_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserServers_Users_UserId",
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
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId1 = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ingredients_ItemOrTags_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ingredients_ItemOrTags_ItemOrTagId1",
                        column: x => x.ItemOrTagId1,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ingredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ingredients_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTagAssocs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTagAssocs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTagAssocs_ItemOrTags_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTagAssocs_ItemOrTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTagAssocs_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId1 = table.Column<Guid>(type: "TEXT", nullable: false),
                    LavishTalent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<float>(type: "REAL", nullable: false),
                    IsStatic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_ItemOrTags_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_ItemOrTags_ItemOrTagId1",
                        column: x => x.ItemOrTagId1,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId1 = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
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
                        name: "FK_UserPrices_ItemOrTags_ItemOrTagId1",
                        column: x => x.ItemOrTagId1,
                        principalTable: "ItemOrTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPrices_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
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
                name: "CraftingTablePluginModules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTablePluginModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraftingTablePluginModules_CraftingTables_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraftingTablePluginModules_PluginModules_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraftingTablePluginModules_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Share = table.Column<float>(type: "REAL", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProducts_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProducts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSkills_ServerId",
                table: "UserSkills",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_PluginModuleId",
                table: "UserCraftingTables",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_ServerId",
                table: "UserCraftingTables",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ServerId",
                table: "Skills",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ServerId",
                table: "Recipes",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTables_ServerId",
                table: "CraftingTables",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTablePluginModules_CraftingTableId",
                table: "CraftingTablePluginModules",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTablePluginModules_PluginModuleId",
                table: "CraftingTablePluginModules",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTablePluginModules_ServerId",
                table: "CraftingTablePluginModules",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_ItemOrTagId",
                table: "Ingredients",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_ItemOrTagId1",
                table: "Ingredients",
                column: "ItemOrTagId1");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_RecipeId",
                table: "Ingredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_ServerId",
                table: "Ingredients",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemOrTags_ItemOrTagId",
                table: "ItemOrTags",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemOrTags_ServerId",
                table: "ItemOrTags",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemOrTags_Tag_ItemOrTagId",
                table: "ItemOrTags",
                column: "Tag_ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagAssocs_ItemId",
                table: "ItemTagAssocs",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagAssocs_ServerId",
                table: "ItemTagAssocs",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTagAssocs_TagId",
                table: "ItemTagAssocs",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginModules_ServerId",
                table: "PluginModules",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ItemOrTagId",
                table: "Products",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ItemOrTagId1",
                table: "Products",
                column: "ItemOrTagId1");

            migrationBuilder.CreateIndex(
                name: "IX_Products_RecipeId",
                table: "Products",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ServerId",
                table: "Products",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrices_ItemOrTagId",
                table: "UserPrices",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrices_ItemOrTagId1",
                table: "UserPrices",
                column: "ItemOrTagId1");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrices_ServerId",
                table: "UserPrices",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPrices_UserId",
                table: "UserPrices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_ProductId",
                table: "UserProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_ServerId",
                table: "UserProducts",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_UserId",
                table: "UserProducts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServers_ServerId",
                table: "UserServers",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServers_UserId",
                table: "UserServers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_ServerId",
                table: "UserSettings",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CraftingTables_Servers_ServerId",
                table: "CraftingTables",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Servers_ServerId",
                table: "Recipes",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Skills_Servers_ServerId",
                table: "Skills",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTables_PluginModules_PluginModuleId",
                table: "UserCraftingTables",
                column: "PluginModuleId",
                principalTable: "PluginModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTables_Servers_ServerId",
                table: "UserCraftingTables",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSkills_Servers_ServerId",
                table: "UserSkills",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CraftingTables_Servers_ServerId",
                table: "CraftingTables");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Servers_ServerId",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Servers_ServerId",
                table: "Skills");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTables_PluginModules_PluginModuleId",
                table: "UserCraftingTables");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCraftingTables_Servers_ServerId",
                table: "UserCraftingTables");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSkills_Servers_ServerId",
                table: "UserSkills");

            migrationBuilder.DropTable(
                name: "CraftingTablePluginModules");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "ItemTagAssocs");

            migrationBuilder.DropTable(
                name: "UserPrices");

            migrationBuilder.DropTable(
                name: "UserProducts");

            migrationBuilder.DropTable(
                name: "UserServers");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "PluginModules");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "ItemOrTags");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_UserSkills_ServerId",
                table: "UserSkills");

            migrationBuilder.DropIndex(
                name: "IX_UserCraftingTables_PluginModuleId",
                table: "UserCraftingTables");

            migrationBuilder.DropIndex(
                name: "IX_UserCraftingTables_ServerId",
                table: "UserCraftingTables");

            migrationBuilder.DropIndex(
                name: "IX_Skills_ServerId",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_ServerId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_CraftingTables_ServerId",
                table: "CraftingTables");

            migrationBuilder.DropColumn(
                name: "HasLavishTalent",
                table: "UserSkills");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "UserSkills");

            migrationBuilder.DropColumn(
                name: "PluginModuleId",
                table: "UserCraftingTables");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "UserCraftingTables");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "CraftMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "FamilyName",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "IsBlueprint",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Labor",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "CraftingTables");

            migrationBuilder.RenameColumn(
                name: "SecretId",
                table: "Users",
                newName: "ProfitMargin");

            migrationBuilder.RenameColumn(
                name: "Pseudo",
                table: "Users",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "RequiredSkillLevel",
                table: "Recipes",
                newName: "MinimumSkillLevel");

            migrationBuilder.RenameColumn(
                name: "IsDefault",
                table: "Recipes",
                newName: "CaloriesRequired");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserSkills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "SkillId",
                table: "UserSkills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "UserSkills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalorieCost",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "UserCraftingTables",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "CraftingTableId",
                table: "UserCraftingTables",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "UserCraftingTables",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "UpgradeId",
                table: "UserCraftingTables",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Skills",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "SkillId",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "CraftingTableId",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CraftingTables",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.CreateTable(
                name: "CraftingTableSkills",
                columns: table => new
                {
                    CraftingTableId = table.Column<int>(type: "INTEGER", nullable: false),
                    SkillId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTableSkills", x => new { x.CraftingTableId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_CraftingTableSkills_CraftingTables_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraftingTableSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFixedQuantity = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeMaterials_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeOutputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputName = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeOutputs_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Upgrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CostReduction = table.Column<float>(type: "REAL", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Upgrades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInputPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInputPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInputPrices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CraftingTableUpgrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CraftingTableId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpgradeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingTableUpgrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraftingTableUpgrades_CraftingTables_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraftingTableUpgrades_Upgrades_UpgradeId",
                        column: x => x.UpgradeId,
                        principalTable: "Upgrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_UpgradeId",
                table: "UserCraftingTables",
                column: "UpgradeId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTableSkills_SkillId",
                table: "CraftingTableSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTableUpgrades_CraftingTableId",
                table: "CraftingTableUpgrades",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingTableUpgrades_UpgradeId",
                table: "CraftingTableUpgrades",
                column: "UpgradeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeMaterials_RecipeId",
                table: "RecipeMaterials",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeOutputs_RecipeId",
                table: "RecipeOutputs",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInputPrices_UserId",
                table: "UserInputPrices",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCraftingTables_Upgrades_UpgradeId",
                table: "UserCraftingTables",
                column: "UpgradeId",
                principalTable: "Upgrades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
