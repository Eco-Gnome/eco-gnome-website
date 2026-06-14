using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class AddServerAutomationPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAutomationPlannerEnabled",
                table: "Server",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAutomationPlannerEnabled",
                table: "Server");
        }
    }
}
