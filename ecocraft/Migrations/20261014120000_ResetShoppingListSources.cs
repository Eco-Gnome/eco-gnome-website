using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20261014120000_ResetShoppingListSources")]
    public partial class ResetShoppingListSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SourceDataContextId was the context a shopping list imported from; it now links the list to it.
            // Existing choices are dropped so no list gets linked without the player asking for it.
            migrationBuilder.Sql("UPDATE \"DataContext\" SET \"SourceDataContextId\" = NULL WHERE \"IsShoppingList\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
