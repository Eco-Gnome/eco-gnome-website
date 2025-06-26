using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addMinAndMaxCaloriesCostAndMargin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaxCalories",
                table: "Server",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxMargin",
                table: "Server",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinCalories",
                table: "Server",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinMargin",
                table: "Server",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxCalories",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MaxMargin",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MinCalories",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MinMargin",
                table: "Server");
        }
    }
}
