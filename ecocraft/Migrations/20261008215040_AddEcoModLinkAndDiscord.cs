using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class AddEcoModLinkAndDiscord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataHash",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoWebAddressOverride",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoWebSecretProtected",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EcoWebUrl",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketCurrency",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketLastError",
                table: "Server",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarketLastFetchAttempt",
                table: "Server",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarketPricesUpdateTime",
                table: "Server",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarketWindowDays",
                table: "Server",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EcoApiToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcoApiToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcoApiToken_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EcoApiToken_UserServer_UserServerId",
                        column: x => x.UserServerId,
                        principalTable: "UserServer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EcoLinkRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    PollTokenHash = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EcoServerId = table.Column<string>(type: "text", nullable: false),
                    EcoServerName = table.Column<string>(type: "text", nullable: false),
                    EcoUserId = table.Column<string>(type: "text", nullable: true),
                    EcoUserName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedUserServerId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcoLinkRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerMarketPrice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemOrTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellAverage = table.Column<decimal>(type: "numeric", nullable: true),
                    SellQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    SellShops = table.Column<int>(type: "integer", nullable: false),
                    BuyAverage = table.Column<decimal>(type: "numeric", nullable: true),
                    BuyQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    BuyShops = table.Column<int>(type: "integer", nullable: false),
                    TradedAverage = table.Column<decimal>(type: "numeric", nullable: true),
                    TradedQuantity = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMarketPrice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerMarketPrice_ItemOrTag_ItemOrTagId",
                        column: x => x.ItemOrTagId,
                        principalTable: "ItemOrTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerMarketPrice_Server_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Server",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogin", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLogin_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserServer_EcoUserId",
                table: "UserServer",
                column: "EcoUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Server_EcoServerId",
                table: "Server",
                column: "EcoServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoApiToken_ServerId",
                table: "EcoApiToken",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoApiToken_TokenHash",
                table: "EcoApiToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcoApiToken_UserServerId",
                table: "EcoApiToken",
                column: "UserServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoLinkRequest_Code",
                table: "EcoLinkRequest",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcoLinkRequest_PollTokenHash",
                table: "EcoLinkRequest",
                column: "PollTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerMarketPrice_ItemOrTagId",
                table: "ServerMarketPrice",
                column: "ItemOrTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMarketPrice_ServerId_ItemOrTagId",
                table: "ServerMarketPrice",
                columns: new[] { "ServerId", "ItemOrTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLogin_Provider_ProviderKey",
                table: "UserLogin",
                columns: new[] { "Provider", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLogin_UserId",
                table: "UserLogin",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EcoApiToken");

            migrationBuilder.DropTable(
                name: "EcoLinkRequest");

            migrationBuilder.DropTable(
                name: "ServerMarketPrice");

            migrationBuilder.DropTable(
                name: "UserLogin");

            migrationBuilder.DropIndex(
                name: "IX_UserServer_EcoUserId",
                table: "UserServer");

            migrationBuilder.DropIndex(
                name: "IX_Server_EcoServerId",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "DataHash",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "EcoWebAddressOverride",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "EcoWebSecretProtected",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "EcoWebUrl",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarketCurrency",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarketLastError",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarketLastFetchAttempt",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarketPricesUpdateTime",
                table: "Server");

            migrationBuilder.DropColumn(
                name: "MarketWindowDays",
                table: "Server");
        }
    }
}
