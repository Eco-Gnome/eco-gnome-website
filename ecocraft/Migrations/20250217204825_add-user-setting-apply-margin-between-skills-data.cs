using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addusersettingapplymarginbetweenskillsdata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE UserSetting
                SET ApplyMarginBetweenSkills = true;
            ");
            migrationBuilder.Sql(@"
                UPDATE UserElement
                SET IsMarginPrice = false;
            ");
        }
    }
}
