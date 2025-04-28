using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addServerInModUploadHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServerId",
                table: "ModUploadHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModUploadHistory_ServerId",
                table: "ModUploadHistory",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModUploadHistory_Server_ServerId",
                table: "ModUploadHistory",
                column: "ServerId",
                principalTable: "Server",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModUploadHistory_Server_ServerId",
                table: "ModUploadHistory");

            migrationBuilder.DropIndex(
                name: "IX_ModUploadHistory_ServerId",
                table: "ModUploadHistory");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "ModUploadHistory");
        }
    }
}
