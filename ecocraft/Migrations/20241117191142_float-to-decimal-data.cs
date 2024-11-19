using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class floattodecimaldata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Skill
                SET LaborReducePercent = (
                    SELECT json_group_array(json_quote(CAST(value as TEXT)))
                    FROM json_each(Skill.LaborReducePercent)
                )
                WHERE LaborReducePercent IS NOT NULL;
            ");

            migrationBuilder.Sql("UPDATE UserSetting SET TimeFee = CAST(ROUND(TimeFee, 2) AS TEXT)");

            migrationBuilder.Sql("UPDATE UserSetting SET CalorieCost = CAST(ROUND(CalorieCost, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE UserPrice SET Price = CAST(ROUND(Price, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE UserPrice SET MarginPrice = CAST(ROUND(MarginPrice, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE UserMargin SET Margin = CAST(ROUND(Margin, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE UserElement SET Share = CAST(ROUND(Share, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE UserElement SET Price = CAST(ROUND(Price, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE UserElement SET MarginPrice = CAST(ROUND(MarginPrice, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE UserCraftingTable SET CraftMinuteFee = CAST(ROUND(CraftMinuteFee, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE Skill SET LavishTalentValue = CAST(ROUND(LavishTalentValue, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE Recipe SET Labor = CAST(ROUND(Labor, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE Recipe SET CraftMinutes = CAST(ROUND(CraftMinutes, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE PluginModule SET Percent = CAST(ROUND(Percent, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE ItemOrTag SET MinPrice = CAST(ROUND(MinPrice, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE ItemOrTag SET MaxPrice = CAST(ROUND(MaxPrice, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE Element SET Quantity = CAST(ROUND(Quantity, 2) AS TEXT);");

            migrationBuilder.Sql("UPDATE Element SET DefaultShare = CAST(ROUND(DefaultShare, 2) AS TEXT);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
