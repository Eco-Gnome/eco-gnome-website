using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class addTalentFinalSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /* Create indexes */
            migrationBuilder.AlterColumn<string>(
                name: "LaborId",
                table: "Recipe",
                type: "TEXT",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "CraftMinutesId",
                table: "Recipe",
                type: "TEXT",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "QuantityId",
                table: "Element",
                type: "TEXT",
                nullable: false);

             migrationBuilder.CreateIndex(
                name: "IX_Recipe_CraftMinutesId",
                table: "Recipe",
                column: "CraftMinutesId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipe_LaborId",
                table: "Recipe",
                column: "LaborId");

            migrationBuilder.CreateIndex(
                name: "IX_Element_QuantityId",
                table: "Element",
                column: "QuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicValue_ServerId",
                table: "DynamicValue",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifier_DynamicValueId",
                table: "Modifier",
                column: "DynamicValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifier_SkillId",
                table: "Modifier",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Modifier_TalentId",
                table: "Modifier",
                column: "TalentId");

            migrationBuilder.CreateIndex(
                name: "IX_Talent_LocalizedNameId",
                table: "Talent",
                column: "LocalizedNameId");

            migrationBuilder.CreateIndex(
                name: "IX_Talent_SkillId",
                table: "Talent",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTalent_TalentId",
                table: "UserTalent",
                column: "TalentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTalent_UserServerId",
                table: "UserTalent",
                column: "UserServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Element_DynamicValue_QuantityId",
                table: "Element",
                column: "QuantityId",
                principalTable: "DynamicValue",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Element_Skill_SkillId",
                table: "Element",
                column: "SkillId",
                principalTable: "Skill",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipe_DynamicValue_CraftMinutesId",
                table: "Recipe",
                column: "CraftMinutesId",
                principalTable: "DynamicValue",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipe_DynamicValue_LaborId",
                table: "Recipe",
                column: "LaborId",
                principalTable: "DynamicValue",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipe_LocalizedField_LocalizedNameId",
                table: "Recipe",
                column: "LocalizedNameId",
                principalTable: "LocalizedField",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            /* Drop old data */
            migrationBuilder.DropForeignKey(
                name: "FK_Element_Skill_SkillId",
                table: "Element");

            migrationBuilder.DropColumn(
                name: "HasLavishTalent",
                table: "UserSkill");

            migrationBuilder.DropColumn(
                name: "LavishTalentValue",
                table: "Skill");

            migrationBuilder.DropColumn(
                name: "IsDynamic",
                table: "Element");

            migrationBuilder.DropColumn(
                name: "LavishTalent",
                table: "Element");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Element");

            migrationBuilder.DropColumn(
                name: "Labor",
                table: "Recipe");

            migrationBuilder.DropColumn(
                name: "CraftMinutes",
                table: "Recipe");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
