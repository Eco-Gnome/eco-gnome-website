using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addvideouploader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasVideoUploader",
                table: "Server",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasVideoUploader",
                table: "Server");
        }
    }
}
