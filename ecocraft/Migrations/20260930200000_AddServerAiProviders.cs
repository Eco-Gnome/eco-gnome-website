using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260930200000_AddServerAiProviders")]
    public partial class AddServerAiProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une clé par fournisseur IA (JSON, ServerAiKey) et le fournisseur actif ; la clé Anthropic existante y est reprise.
            migrationBuilder.AddColumn<string>(name: "AiProvider", table: "Server", type: "text", nullable: false, defaultValue: "anthropic");
            migrationBuilder.AddColumn<string>(name: "AiKeysJson", table: "Server", type: "text", nullable: true);
            migrationBuilder.Sql("""
                UPDATE "Server" SET "AiKeysJson" = json_build_array(json_build_object(
                    'provider', 'anthropic', 'keyProtected', "AiApiKeyProtected", 'userId', "AiApiKeyUserId",
                    'validatedAt', COALESCE("AiApiKeyValidatedAt", now()), 'openToPlayers', "IsAiOpenToPlayers", 'model', 'claude-opus-5-5'))::text
                WHERE "AiApiKeyProtected" IS NOT NULL AND "AiApiKeyUserId" IS NOT NULL;
                """);
            migrationBuilder.DropColumn(name: "AiApiKeyProtected", table: "Server");
            migrationBuilder.DropColumn(name: "AiApiKeyUserId", table: "Server");
            migrationBuilder.DropColumn(name: "AiApiKeyValidatedAt", table: "Server");
            migrationBuilder.DropColumn(name: "IsAiOpenToPlayers", table: "Server");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seule la clé Anthropic revient dans les anciennes colonnes.
            migrationBuilder.AddColumn<string>(name: "AiApiKeyProtected", table: "Server", type: "text", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "AiApiKeyUserId", table: "Server", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "AiApiKeyValidatedAt", table: "Server", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "IsAiOpenToPlayers", table: "Server", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.Sql("""
                UPDATE "Server" SET "AiApiKeyProtected" = s.k->>'keyProtected', "AiApiKeyUserId" = (s.k->>'userId')::uuid,
                    "AiApiKeyValidatedAt" = (s.k->>'validatedAt')::timestamptz, "IsAiOpenToPlayers" = (s.k->>'openToPlayers')::boolean
                FROM (SELECT "Id" AS id, e AS k FROM "Server", json_array_elements("AiKeysJson"::json) e WHERE e->>'provider' = 'anthropic') s
                WHERE "Server"."Id" = s.id;
                """);
            migrationBuilder.DropColumn(name: "AiProvider", table: "Server");
            migrationBuilder.DropColumn(name: "AiKeysJson", table: "Server");
        }
    }
}
