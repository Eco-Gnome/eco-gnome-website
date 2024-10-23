using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addUserMarginclass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMargin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Margin = table.Column<float>(type: "REAL", nullable: false),
                    UserSettingId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMargin", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMargin_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMargin_UserSetting_UserSettingId",
                        column: x => x.UserSettingId,
                        principalTable: "UserSetting",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMargin_UserServerId",
                table: "UserMargin",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMargin_UserSettingId",
                table: "UserMargin",
                column: "UserSettingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMargin");
        }
    }
}
