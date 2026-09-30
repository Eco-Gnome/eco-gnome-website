using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260926120000_AddServerAiGenerationEnabled")]
    public partial class AddServerAiGenerationEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Génération IA autorisée par un super-admin, serveur par serveur.
            migrationBuilder.AddColumn<bool>(name: "IsAiGenerationEnabled", table: "Server", type: "boolean", nullable: false, defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsAiGenerationEnabled", table: "Server");
        }
    }
}
