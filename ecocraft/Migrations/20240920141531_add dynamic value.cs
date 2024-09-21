using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class adddynamicvalue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsStatic",
                table: "Products",
                newName: "IsDynamic");

            migrationBuilder.AddColumn<bool>(
                name: "IsDynamic",
                table: "Ingredients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDynamic",
                table: "Ingredients");

            migrationBuilder.RenameColumn(
                name: "IsDynamic",
                table: "Products",
                newName: "IsStatic");
        }
    }
}
