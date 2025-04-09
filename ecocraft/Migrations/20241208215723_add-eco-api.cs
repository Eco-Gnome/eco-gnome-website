using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addEcoApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EcoUserId",
                table: "UserServer",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoServerId",
                table: "Server",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EcoUserId",
                table: "UserServer");

            migrationBuilder.DropColumn(
                name: "EcoServerId",
                table: "Server");
        }
    }
}
