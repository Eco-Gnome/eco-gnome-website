using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class AddTalentBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cap",
                table: "Talent");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Talent");

            migrationBuilder.CreateTable(
                name: "TalentBonus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    EffectType = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Cap = table.Column<decimal>(type: "numeric", nullable: true),
                    ItemTags = table.Column<string[]>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalentBonus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalentBonus_Talent_TalentId",
                        column: x => x.TalentId,
                        principalTable: "Talent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalentBonus_TalentId",
                table: "TalentBonus",
                column: "TalentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TalentBonus");

            migrationBuilder.AddColumn<decimal>(
                name: "Cap",
                table: "Talent",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "Talent",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
