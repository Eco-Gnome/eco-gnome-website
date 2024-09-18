using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class usercraftingtable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserUpgrades");

            migrationBuilder.CreateTable(
                name: "UserCraftingTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CraftingTableId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpgradeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCraftingTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCraftingTables_CraftingTables_CraftingTableId",
                        column: x => x.CraftingTableId,
                        principalTable: "CraftingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTables_Upgrades_UpgradeId",
                        column: x => x.UpgradeId,
                        principalTable: "Upgrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCraftingTables_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_CraftingTableId",
                table: "UserCraftingTables",
                column: "CraftingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_UpgradeId",
                table: "UserCraftingTables",
                column: "UpgradeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCraftingTables_UserId",
                table: "UserCraftingTables",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCraftingTables");

            migrationBuilder.CreateTable(
                name: "UserUpgrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UpgradeId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonalReduction = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUpgrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserUpgrades_Upgrades_UpgradeId",
                        column: x => x.UpgradeId,
                        principalTable: "Upgrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserUpgrades_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserUpgrades_UpgradeId",
                table: "UserUpgrades",
                column: "UpgradeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserUpgrades_UserId",
                table: "UserUpgrades",
                column: "UserId");
        }
    }
}
