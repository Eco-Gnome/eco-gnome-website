using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addshoppinglist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShoppingList",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingList", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingList_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingListCraftingTable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginModuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ShoppingListId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListCraftingTable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListCraftingTable_CraftingTable_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListCraftingTable_PluginModule_PluginModuleId",
                        column: x => x.PluginModuleId,
                        principalTable: "PluginModule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListCraftingTable_ShoppingList_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingListSkill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShoppingListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    HasLavishTalent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListSkill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListSkill_ShoppingList_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListSkill_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingListRecipe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShoppingListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentShoppingListRecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ShoppingListCraftingTableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShoppingListSkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    QuantityToCraft = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListRecipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListRecipe_Recipe_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListRecipe_ShoppingListCraftingTable_ShoppingListCraftingTableId",
                        column: x => x.ShoppingListCraftingTableId,
                        principalTable: "ShoppingListCraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListRecipe_ShoppingListRecipe_ParentShoppingListRecipeId",
                        column: x => x.ParentShoppingListRecipeId,
                        principalTable: "ShoppingListRecipe",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShoppingListRecipe_ShoppingListSkill_ShoppingListSkillId",
                        column: x => x.ShoppingListSkillId,
                        principalTable: "ShoppingListSkill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListRecipe_ShoppingList_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingListItemOrTag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShoppingListRecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsIngredient = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListItemOrTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListItemOrTag_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListItemOrTag_ShoppingListRecipe_ShoppingListRecipeId",
                        column: x => x.ShoppingListRecipeId,
                        principalTable: "ShoppingListRecipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingList_UserServerId",
                table: "ShoppingList",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListCraftingTable_CraftingTableId",
                table: "ShoppingListCraftingTable",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListCraftingTable_PluginModuleId",
                table: "ShoppingListCraftingTable",
                column: "PluginModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListCraftingTable_ShoppingListId",
                table: "ShoppingListCraftingTable",
                column: "ShoppingListId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItemOrTag_ItemOrTagId",
                table: "ShoppingListItemOrTag",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItemOrTag_ShoppingListRecipeId",
                table: "ShoppingListItemOrTag",
                column: "ShoppingListRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListRecipe_ParentShoppingListRecipeId",
                table: "ShoppingListRecipe",
                column: "ParentShoppingListRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListRecipe_RecipeId",
                table: "ShoppingListRecipe",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListRecipe_ShoppingListCraftingTableId",
                table: "ShoppingListRecipe",
                column: "ShoppingListCraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListRecipe_ShoppingListId",
                table: "ShoppingListRecipe",
                column: "ShoppingListId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListRecipe_ShoppingListSkillId",
                table: "ShoppingListRecipe",
                column: "ShoppingListSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListSkill_ShoppingListId",
                table: "ShoppingListSkill",
                column: "ShoppingListId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListSkill_SkillId",
                table: "ShoppingListSkill",
                column: "SkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoppingListItemOrTag");

            migrationBuilder.DropTable(
                name: "ShoppingListRecipe");

            migrationBuilder.DropTable(
                name: "ShoppingListCraftingTable");

            migrationBuilder.DropTable(
                name: "ShoppingListSkill");

            migrationBuilder.DropTable(
                name: "ShoppingList");

            migrationBuilder.CreateTable(
                name: "UserShoppingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShoppingLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShoppingLists_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserShoppingListElements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserShoppingListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuantityNeeded = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShoppingListElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShoppingListElements_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListElements_UserShoppingLists_UserShoppingListId",
                        column: x => x.UserShoppingListId,
                        principalTable: "UserShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserShoppingListRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentRecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserCraftingTableId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserShoppingListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuantityToCraft = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShoppingListRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipes_Recipe_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipes_UserCraftingTable_UserCraftingTableId",
                        column: x => x.UserCraftingTableId,
                        principalTable: "UserCraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipes_UserShoppingListRecipes_ParentRecipeId",
                        column: x => x.ParentRecipeId,
                        principalTable: "UserShoppingListRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipes_UserShoppingLists_UserShoppingListId",
                        column: x => x.UserShoppingListId,
                        principalTable: "UserShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListElements_ItemOrTagId",
                table: "UserShoppingListElements",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListElements_UserShoppingListId",
                table: "UserShoppingListElements",
                column: "UserShoppingListId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipes_ParentRecipeId",
                table: "UserShoppingListRecipes",
                column: "ParentRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipes_RecipeId",
                table: "UserShoppingListRecipes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipes_UserCraftingTableId",
                table: "UserShoppingListRecipes",
                column: "UserCraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipes_UserShoppingListId",
                table: "UserShoppingListRecipes",
                column: "UserShoppingListId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingLists_UserServerId",
                table: "UserShoppingLists",
                column: "UserServerId");
        }
    }
}
