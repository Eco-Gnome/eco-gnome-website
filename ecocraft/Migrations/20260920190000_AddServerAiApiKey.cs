using System;
using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260920190000_AddServerAiApiKey")]
    public partial class AddServerAiApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clé Anthropic par serveur (chiffrée), son propriétaire, sa date de validation, ouverture aux joueurs.
            migrationBuilder.AddColumn<string>(name: "AiApiKeyProtected", table: "Server", type: "text", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "AiApiKeyUserId", table: "Server", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "AiApiKeyValidatedAt", table: "Server", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "IsAiOpenToPlayers", table: "Server", type: "boolean", nullable: false, defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AiApiKeyProtected", table: "Server");
            migrationBuilder.DropColumn(name: "AiApiKeyUserId", table: "Server");
            migrationBuilder.DropColumn(name: "AiApiKeyValidatedAt", table: "Server");
            migrationBuilder.DropColumn(name: "IsAiOpenToPlayers", table: "Server");
        }
    }
}
