using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class updateitemtagrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_ItemOrTags_ItemOrTagId1",
                table: "Ingredients");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPrices_ItemOrTags_ItemOrTagId1",
                table: "UserPrices");

            migrationBuilder.DropIndex(
                name: "IX_UserPrices_ItemOrTagId1",
                table: "UserPrices");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_ItemOrTagId1",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "ItemOrTagId1",
                table: "UserPrices");

            migrationBuilder.DropColumn(
                name: "ItemOrTagId1",
                table: "Ingredients");

            migrationBuilder.AddColumn<Guid>(
                name: "TagId",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_TagId",
                table: "Products",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ItemOrTags_TagId",
                table: "Products",
                column: "TagId",
                principalTable: "ItemOrTags",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ItemOrTags_TagId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TagId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TagId",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "ItemOrTagId1",
                table: "UserPrices",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ItemOrTagId1",
                table: "Ingredients",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_UserPrices_ItemOrTagId1",
                table: "UserPrices",
                column: "ItemOrTagId1");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_ItemOrTagId1",
                table: "Ingredients",
                column: "ItemOrTagId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_ItemOrTags_ItemOrTagId1",
                table: "Ingredients",
                column: "ItemOrTagId1",
                principalTable: "ItemOrTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPrices_ItemOrTags_ItemOrTagId1",
                table: "UserPrices",
                column: "ItemOrTagId1",
                principalTable: "ItemOrTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
