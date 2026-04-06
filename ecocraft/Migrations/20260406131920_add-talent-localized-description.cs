using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addtalentlocalizeddescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocalizedDescriptionId",
                table: "Talent",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Talent_LocalizedDescriptionId",
                table: "Talent",
                column: "LocalizedDescriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Talent_LocalizedField_LocalizedDescriptionId",
                table: "Talent",
                column: "LocalizedDescriptionId",
                principalTable: "LocalizedField",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Talent_LocalizedField_LocalizedDescriptionId",
                table: "Talent");

            migrationBuilder.DropIndex(
                name: "IX_Talent_LocalizedDescriptionId",
                table: "Talent");

            migrationBuilder.DropColumn(
                name: "LocalizedDescriptionId",
                table: "Talent");
        }
    }
}
