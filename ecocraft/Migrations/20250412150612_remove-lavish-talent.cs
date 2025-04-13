using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class removelavishtalent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasLavishTalent",
                table: "ShoppingListSkill");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasLavishTalent",
                table: "ShoppingListSkill",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
