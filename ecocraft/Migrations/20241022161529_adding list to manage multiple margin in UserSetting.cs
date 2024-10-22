using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addinglisttomanagemultiplemargininUserSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarginNames",
                table: "UserSetting",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "MarginValues",
                table: "UserSetting",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarginNames",
                table: "UserSetting");

            migrationBuilder.DropColumn(
                name: "MarginValues",
                table: "UserSetting");
        }
    }
}
