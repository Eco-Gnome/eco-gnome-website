using System;
using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20261008120000_AddDataContextSource")]
    public partial class AddDataContextSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contexte du price calculator d'où une shopping list a importé métiers, talents et tables.
            migrationBuilder.AddColumn<Guid>(name: "SourceDataContextId", table: "DataContext", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_DataContext_SourceDataContextId", table: "DataContext", column: "SourceDataContextId");
            migrationBuilder.AddForeignKey(
                name: "FK_DataContext_DataContext_SourceDataContextId",
                table: "DataContext",
                column: "SourceDataContextId",
                principalTable: "DataContext",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_DataContext_DataContext_SourceDataContextId", table: "DataContext");
            migrationBuilder.DropIndex(name: "IX_DataContext_SourceDataContextId", table: "DataContext");
            migrationBuilder.DropColumn(name: "SourceDataContextId", table: "DataContext");
        }
    }
}
