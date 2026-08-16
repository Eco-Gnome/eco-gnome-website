using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260816120000_AddUserLastActionDateTime")]
    public partial class AddUserLastActionDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pas de backfill : les comptes existants restent à NULL et la date ne s'écrit que lorsque
            // quelqu'un interagit réellement. Deviner qui est humain à partir de son profil (renommé,
            // a un métier, admin d'un serveur…) laisserait passer les joueurs qui n'ont que des prix,
            // et une purge les supprimerait en silence. Ici rien n'est supposé.
            // Contrepartie : jusqu'à ce que les joueurs repassent, NULL veut dire « pas encore revu »
            // autant que « robot ». C'est CreationDateTime qui fait la différence — voir la chip
            // « Jamais agi » de la page super admin, qui exige un compte assez ancien.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActionDateTime",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastActionDateTime",
                table: "User");
        }
    }
}
