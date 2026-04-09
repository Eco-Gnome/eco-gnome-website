using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260410_AddServerCalorieCostPolicy")]
    public partial class AddServerCalorieCostPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CalorieCostDefault",
                table: "Server",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalorieCostMax",
                table: "Server",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalorieCostMin",
                table: "Server",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCalorieCostLocked",
                table: "Server",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LockedCalorieCost",
                table: "Server",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalorieCostDefault",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "CalorieCostMax",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "CalorieCostMin",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "IsCalorieCostLocked",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "LockedCalorieCost",
                table: "Server");
        }
    }
}
