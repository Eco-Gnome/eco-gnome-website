using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class updateproduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ItemOrTags_ItemOrTagId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_ItemOrTags_ItemOrTagId1",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ItemOrTagId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ItemOrTagId",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "ItemOrTagId1",
                table: "Products",
                newName: "ItemId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_ItemOrTagId1",
                table: "Products",
                newName: "IX_Products_ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ItemOrTags_ItemId",
                table: "Products",
                column: "ItemId",
                principalTable: "ItemOrTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ItemOrTags_ItemId",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "ItemId",
                table: "Products",
                newName: "ItemOrTagId1");

            migrationBuilder.RenameIndex(
                name: "IX_Products_ItemId",
                table: "Products",
                newName: "IX_Products_ItemOrTagId1");

            migrationBuilder.AddColumn<Guid>(
                name: "ItemOrTagId",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Products_ItemOrTagId",
                table: "Products",
                column: "ItemOrTagId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ItemOrTags_ItemOrTagId",
                table: "Products",
                column: "ItemOrTagId",
                principalTable: "ItemOrTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ItemOrTags_ItemOrTagId1",
                table: "Products",
                column: "ItemOrTagId1",
                principalTable: "ItemOrTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
