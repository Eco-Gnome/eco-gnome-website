using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class createshoppinglistmodels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserShoppingList",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShoppingList", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShoppingList_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserShoppingListElement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserShoppingListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuantityNeeded = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShoppingListElement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShoppingListElement_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListElement_UserShoppingList_UserShoppingListId",
                        column: x => x.UserShoppingListId,
                        principalTable: "UserShoppingList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserShoppingListRecipe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserShoppingListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuantityToCraft = table.Column<decimal>(type: "TEXT", nullable: false),
                    UserCraftingTableId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentRecipeId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShoppingListRecipe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipe_Recipe_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipe_UserCraftingTable_UserCraftingTableId",
                        column: x => x.UserCraftingTableId,
                        principalTable: "UserCraftingTable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipe_UserShoppingListRecipe_ParentRecipeId",
                        column: x => x.ParentRecipeId,
                        principalTable: "UserShoppingListRecipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserShoppingListRecipe_UserShoppingList_UserShoppingListId",
                        column: x => x.UserShoppingListId,
                        principalTable: "UserShoppingList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingList_UserServerId",
                table: "UserShoppingList",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListElement_ItemOrTagId",
                table: "UserShoppingListElement",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListElement_UserShoppingListId",
                table: "UserShoppingListElement",
                column: "UserShoppingListId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipe_ParentRecipeId",
                table: "UserShoppingListRecipe",
                column: "ParentRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipe_RecipeId",
                table: "UserShoppingListRecipe",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipe_UserCraftingTableId",
                table: "UserShoppingListRecipe",
                column: "UserCraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_UserShoppingListRecipe_UserShoppingListId",
                table: "UserShoppingListRecipe",
                column: "UserShoppingListId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserShoppingListElement");

            migrationBuilder.DropTable(
                name: "UserShoppingListRecipe");

            migrationBuilder.DropTable(
                name: "UserShoppingList");
        }
    }
}
