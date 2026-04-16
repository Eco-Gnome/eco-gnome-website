using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260416_AddServerMarginPolicy")]
    public partial class AddServerMarginPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMarginLocked",
                table: "Server",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LockedMargin",
                table: "Server",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarginDefault",
                table: "Server",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarginMax",
                table: "Server",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarginMin",
                table: "Server",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMarginLocked",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "LockedMargin",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarginDefault",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarginMax",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarginMin",
                table: "Server");
        }
    }
}
