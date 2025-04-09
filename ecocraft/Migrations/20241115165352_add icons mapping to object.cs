using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addIconsMappingToObject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "imageFile",
                table: "Skill",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "posX",
                table: "Skill",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "posY",
                table: "Skill",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "imageFile",
                table: "ItemOrTag",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "posX",
                table: "ItemOrTag",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "posY",
                table: "ItemOrTag",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "imageFile",
                table: "CraftingTable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "posX",
                table: "CraftingTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "posY",
                table: "CraftingTable",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "imageFile",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "posX",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "posY",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "imageFile",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "posX",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "posY",
                table: "ItemOrTag");

            migrationBuilder.DropColumn(
                name: "imageFile",
                table: "CraftingTable");

            migrationBuilder.DropColumn(
                name: "posX",
                table: "CraftingTable");

            migrationBuilder.DropColumn(
                name: "posY",
                table: "CraftingTable");
        }
    }
}
