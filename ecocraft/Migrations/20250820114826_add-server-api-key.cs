using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addServerApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApiKey",
                table: "Server",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE Server SET ApiKey=upper(hex(randomblob(4))) || '-' ||
                                                            upper(hex(randomblob(2))) || '-' ||
                                                            '4' || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                                            substr('89AB', abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                                            upper(hex(randomblob(6))) WHERE true");
            
            migrationBuilder.AlterColumn<Guid>(
                name: "ApiKey",
                table: "Server",
                type: "TEXT",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "Server");
        }
    }
}
