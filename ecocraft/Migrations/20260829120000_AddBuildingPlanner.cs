using System;
using ecocraft.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EcoCraftDbContext))]
    [Migration("20260829120000_AddBuildingPlanner")]
    public partial class AddBuildingPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Données « bâtiment » de l'export v5 (EcoGnomeMod) : emprise et drapeaux des objets posables,
            // tiers des blocs de construction, configs pièces/housing du serveur. Toutes nullables ou à
            // défaut : un serveur importé en v4 garde des colonnes vides et HasBuildingData = false.
            migrationBuilder.AddColumn<int>(
                name: "BlockTier",
                table: "ItemOrTag",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BlockHasForms",
                table: "ItemOrTag",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BlockIgnoreRooms",
                table: "ItemOrTag",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BlockIsRoomMaterialOption",
                table: "ItemOrTag",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BlockIsWall",
                table: "ItemOrTag",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorldObjectAttachedSide",
                table: "ItemOrTag",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WorldObjectCanBeOnSurface",
                table: "ItemOrTag",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WorldObjectHasTableSurface",
                table: "ItemOrTag",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WorldObjectMustBeGridAligned",
                table: "ItemOrTag",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WorldObjectOccupancyIsDefault",
                table: "ItemOrTag",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WorldObjectOccupancyJson",
                table: "ItemOrTag",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorldObjectTier",
                table: "ItemOrTag",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WorldObjectWallMounted",
                table: "ItemOrTag",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BuildingConfigJson",
                table: "Server",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBuildingData",
                table: "Server",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HousingConfigJson",
                table: "Server",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BuildingPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Document = table.Column<string>(type: "jsonb", nullable: false),
                    CreationDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingPlan_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingPlan_UserServerId",
                table: "BuildingPlan",
                column: "UserServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingPlan");

            migrationBuilder.DropColumn(name: "BlockTier", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "BlockHasForms", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "BlockIgnoreRooms", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "BlockIsRoomMaterialOption", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "BlockIsWall", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectAttachedSide", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectCanBeOnSurface", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectHasTableSurface", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectMustBeGridAligned", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectOccupancyIsDefault", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectOccupancyJson", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectTier", table: "ItemOrTag");
            migrationBuilder.DropColumn(name: "WorldObjectWallMounted", table: "ItemOrTag");

            migrationBuilder.DropColumn(name: "BuildingConfigJson", table: "Server");
            migrationBuilder.DropColumn(name: "HasBuildingData", table: "Server");
            migrationBuilder.DropColumn(name: "HousingConfigJson", table: "Server");
        }
    }
}
