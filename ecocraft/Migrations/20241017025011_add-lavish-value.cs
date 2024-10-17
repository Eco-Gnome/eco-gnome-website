using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addlavishvalue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "LavishTalentValue",
                table: "Skill",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LavishTalentValue",
                table: "Skill");
        }
    }
}
