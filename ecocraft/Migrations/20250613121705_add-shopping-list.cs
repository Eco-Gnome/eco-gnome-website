using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentUserRecipeId",
                table: "UserRecipe",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserRecipeId",
                table: "UserElement",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE UserElement
                SET UserRecipeId = (
                    SELECT ur.Id
                    FROM Element e
                    JOIN UserRecipe ur 
                        ON ur.DataContextId = UserElement.DataContextId 
                        AND ur.RecipeId = e.RecipeId
                    WHERE e.Id = UserElement.ElementId
                );
            ");

            migrationBuilder.Sql(@"DELETE FROM UserElement Where UserRecipeId is null");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserRecipeId",
                table: "UserElement",
                type: "TEXT",
                nullable: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsShoppingList",
                table: "DataContext",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserRecipe_ParentUserRecipeId",
                table: "UserRecipe",
                column: "ParentUserRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserElement_UserRecipeId",
                table: "UserElement",
                column: "UserRecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserElement_UserRecipe_UserRecipeId",
                table: "UserElement",
                column: "UserRecipeId",
                principalTable: "UserRecipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRecipe_UserRecipe_ParentUserRecipeId",
                table: "UserRecipe",
                column: "ParentUserRecipeId",
                principalTable: "UserRecipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserElement_UserRecipe_UserRecipeId",
                table: "UserElement");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRecipe_UserRecipe_ParentUserRecipeId",
                table: "UserRecipe");

            migrationBuilder.DropIndex(
                name: "IX_UserRecipe_ParentUserRecipeId",
                table: "UserRecipe");

            migrationBuilder.DropIndex(
                name: "IX_UserElement_UserRecipeId",
                table: "UserElement");

            migrationBuilder.DropColumn(
                name: "ParentUserRecipeId",
                table: "UserRecipe");

            migrationBuilder.DropColumn(
                name: "UserRecipeId",
                table: "UserElement");

            migrationBuilder.DropColumn(
                name: "IsShoppingList",
                table: "DataContext");
        }
    }
}
