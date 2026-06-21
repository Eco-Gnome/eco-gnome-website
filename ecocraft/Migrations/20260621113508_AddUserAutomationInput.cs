using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAutomationInput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAutomationInput",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataContextId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cap = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAutomationInput", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAutomationInput_DataContext_DataContextId",
                        column: x => x.DataContextId,
                        principalTable: "DataContext",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserAutomationInput_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAutomationInput_DataContextId",
                table: "UserAutomationInput",
                column: "DataContextId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAutomationInput_ItemOrTagId",
                table: "UserAutomationInput",
                column: "ItemOrTagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAutomationInput");
        }
    }
}
