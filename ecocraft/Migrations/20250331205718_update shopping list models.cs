using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class updateshoppinglistmodels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingList_UserServer_UserServerId",
                table: "UserShoppingList");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListElement_ItemOrTag_ItemOrTagId",
                table: "UserShoppingListElement");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListElement_UserShoppingList_UserShoppingListId",
                table: "UserShoppingListElement");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipe_Recipe_RecipeId",
                table: "UserShoppingListRecipe");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipe_UserCraftingTable_UserCraftingTableId",
                table: "UserShoppingListRecipe");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipe_UserShoppingListRecipe_ParentRecipeId",
                table: "UserShoppingListRecipe");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipe_UserShoppingList_UserShoppingListId",
                table: "UserShoppingListRecipe");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserShoppingListRecipe",
                table: "UserShoppingListRecipe");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserShoppingListElement",
                table: "UserShoppingListElement");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserShoppingList",
                table: "UserShoppingList");

            migrationBuilder.RenameTable(
                name: "UserShoppingListRecipe",
                newName: "UserShoppingListRecipes");

            migrationBuilder.RenameTable(
                name: "UserShoppingListElement",
                newName: "UserShoppingListElements");

            migrationBuilder.RenameTable(
                name: "UserShoppingList",
                newName: "UserShoppingLists");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipe_UserShoppingListId",
                table: "UserShoppingListRecipes",
                newName: "IX_UserShoppingListRecipes_UserShoppingListId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipe_UserCraftingTableId",
                table: "UserShoppingListRecipes",
                newName: "IX_UserShoppingListRecipes_UserCraftingTableId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipe_RecipeId",
                table: "UserShoppingListRecipes",
                newName: "IX_UserShoppingListRecipes_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipe_ParentRecipeId",
                table: "UserShoppingListRecipes",
                newName: "IX_UserShoppingListRecipes_ParentRecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListElement_UserShoppingListId",
                table: "UserShoppingListElements",
                newName: "IX_UserShoppingListElements_UserShoppingListId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListElement_ItemOrTagId",
                table: "UserShoppingListElements",
                newName: "IX_UserShoppingListElements_ItemOrTagId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingList_UserServerId",
                table: "UserShoppingLists",
                newName: "IX_UserShoppingLists_UserServerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserShoppingListRecipes",
                table: "UserShoppingListRecipes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserShoppingListElements",
                table: "UserShoppingListElements",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserShoppingLists",
                table: "UserShoppingLists",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListElements_ItemOrTag_ItemOrTagId",
                table: "UserShoppingListElements",
                column: "ItemOrTagId",
                principalTable: "ItemOrTag",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListElements_UserShoppingLists_UserShoppingListId",
                table: "UserShoppingListElements",
                column: "UserShoppingListId",
                principalTable: "UserShoppingLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipes_Recipe_RecipeId",
                table: "UserShoppingListRecipes",
                column: "RecipeId",
                principalTable: "Recipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipes_UserCraftingTable_UserCraftingTableId",
                table: "UserShoppingListRecipes",
                column: "UserCraftingTableId",
                principalTable: "UserCraftingTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipes_UserShoppingListRecipes_ParentRecipeId",
                table: "UserShoppingListRecipes",
                column: "ParentRecipeId",
                principalTable: "UserShoppingListRecipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipes_UserShoppingLists_UserShoppingListId",
                table: "UserShoppingListRecipes",
                column: "UserShoppingListId",
                principalTable: "UserShoppingLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingLists_UserServer_UserServerId",
                table: "UserShoppingLists",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListElements_ItemOrTag_ItemOrTagId",
                table: "UserShoppingListElements");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListElements_UserShoppingLists_UserShoppingListId",
                table: "UserShoppingListElements");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipes_Recipe_RecipeId",
                table: "UserShoppingListRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipes_UserCraftingTable_UserCraftingTableId",
                table: "UserShoppingListRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipes_UserShoppingListRecipes_ParentRecipeId",
                table: "UserShoppingListRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingListRecipes_UserShoppingLists_UserShoppingListId",
                table: "UserShoppingListRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserShoppingLists_UserServer_UserServerId",
                table: "UserShoppingLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserShoppingLists",
                table: "UserShoppingLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserShoppingListRecipes",
                table: "UserShoppingListRecipes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserShoppingListElements",
                table: "UserShoppingListElements");

            migrationBuilder.RenameTable(
                name: "UserShoppingLists",
                newName: "UserShoppingList");

            migrationBuilder.RenameTable(
                name: "UserShoppingListRecipes",
                newName: "UserShoppingListRecipe");

            migrationBuilder.RenameTable(
                name: "UserShoppingListElements",
                newName: "UserShoppingListElement");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingLists_UserServerId",
                table: "UserShoppingList",
                newName: "IX_UserShoppingList_UserServerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipes_UserShoppingListId",
                table: "UserShoppingListRecipe",
                newName: "IX_UserShoppingListRecipe_UserShoppingListId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipes_UserCraftingTableId",
                table: "UserShoppingListRecipe",
                newName: "IX_UserShoppingListRecipe_UserCraftingTableId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipes_RecipeId",
                table: "UserShoppingListRecipe",
                newName: "IX_UserShoppingListRecipe_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListRecipes_ParentRecipeId",
                table: "UserShoppingListRecipe",
                newName: "IX_UserShoppingListRecipe_ParentRecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListElements_UserShoppingListId",
                table: "UserShoppingListElement",
                newName: "IX_UserShoppingListElement_UserShoppingListId");

            migrationBuilder.RenameIndex(
                name: "IX_UserShoppingListElements_ItemOrTagId",
                table: "UserShoppingListElement",
                newName: "IX_UserShoppingListElement_ItemOrTagId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserShoppingList",
                table: "UserShoppingList",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserShoppingListRecipe",
                table: "UserShoppingListRecipe",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserShoppingListElement",
                table: "UserShoppingListElement",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingList_UserServer_UserServerId",
                table: "UserShoppingList",
                column: "UserServerId",
                principalTable: "UserServer",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListElement_ItemOrTag_ItemOrTagId",
                table: "UserShoppingListElement",
                column: "ItemOrTagId",
                principalTable: "ItemOrTag",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListElement_UserShoppingList_UserShoppingListId",
                table: "UserShoppingListElement",
                column: "UserShoppingListId",
                principalTable: "UserShoppingList",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipe_Recipe_RecipeId",
                table: "UserShoppingListRecipe",
                column: "RecipeId",
                principalTable: "Recipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipe_UserCraftingTable_UserCraftingTableId",
                table: "UserShoppingListRecipe",
                column: "UserCraftingTableId",
                principalTable: "UserCraftingTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipe_UserShoppingListRecipe_ParentRecipeId",
                table: "UserShoppingListRecipe",
                column: "ParentRecipeId",
                principalTable: "UserShoppingListRecipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserShoppingListRecipe_UserShoppingList_UserShoppingListId",
                table: "UserShoppingListRecipe",
                column: "UserShoppingListId",
                principalTable: "UserShoppingList",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
