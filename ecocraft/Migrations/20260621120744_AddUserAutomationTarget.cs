using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAutomationTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAutomationTarget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    IsMax = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAutomationTarget", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAutomationTarget_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserAutomationTarget_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAutomationTarget_DataContextId",
                table: "UserAutomationTarget",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAutomationTarget_ItemOrTagId",
                table: "UserAutomationTarget",
                column: "ItemOrTagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAutomationTarget");
        }
    }
}
