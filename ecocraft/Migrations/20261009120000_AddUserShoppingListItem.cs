using System;
using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20261009120000_AddUserShoppingListItem")]
    public partial class AddUserShoppingListItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stock déjà possédé et objet choisi pour un tag, par ligne « À acheter » d'une shopping list.
            migrationBuilder.CreateTable(
                name: "UserShoppingListItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChosenItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stock = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserShoppingListItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserShoppingListItem_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserShoppingListItem_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserShoppingListItem_ItemOrTag_ChosenItemId",
                        column: x => x.ChosenItemId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(name: "IX_UserShoppingListItem_DataContextId", table: "UserShoppingListItem", column: "DataContextId");
            migrationBuilder.CreateIndex(name: "IX_UserShoppingListItem_ItemOrTagId", table: "UserShoppingListItem", column: "ItemOrTagId");
            migrationBuilder.CreateIndex(name: "IX_UserShoppingListItem_ChosenItemId", table: "UserShoppingListItem", column: "ChosenItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserShoppingListItem");
        }
    }
}
