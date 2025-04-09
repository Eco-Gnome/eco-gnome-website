using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ecocraft.Migrations
{
    /// <inheritdoc />
    public partial class migrateTalentData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Element SET QuantityId = upper(hex(randomblob(4))) || '-' ||
                                                upper(hex(randomblob(2))) || '-' ||
                                                '4' || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                                substr('89AB', abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                                upper(hex(randomblob(6))) WHERE true;

                INSERT INTO DynamicValue (Id, BaseValue, ServerId)
                    SELECT e.QuantityId, e.Quantity, r.ServerId FROM ELEMENT e
                    INNER JOIN Recipe r on e.RecipeId = r.Id;


                INSERT INTO Modifier (Id, DynamicType, DynamicValueId, SkillId)
                    SELECT
                        upper(hex(randomblob(4))) || '-' ||
                        upper(hex(randomblob(2))) || '-' ||
                        '4' || substr(upper(hex(randomblob(2))), 2) || '-' ||
                        substr('89AB', abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))), 2) || '-' ||
                        upper(hex(randomblob(6))),
                        'Module',
                        e.QuantityId,
                        e.SkillId
                    FROM Element e WHERE e.IsDynamic = 1;
            ");

            migrationBuilder.Sql(@"
                UPDATE Recipe SET CraftMinutesId = upper(hex(randomblob(4))) || '-' ||
                                                upper(hex(randomblob(2))) || '-' ||
                                                '4' || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                                substr('89AB', abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                                upper(hex(randomblob(6))) WHERE true;

                INSERT INTO DynamicValue (Id, BaseValue, ServerId)
                    SELECT r.CraftMinutesId, r.CraftMinutes, r.ServerId FROM Recipe r;

                INSERT INTO Modifier (Id, DynamicType, DynamicValueId, SkillId)
                    SELECT
                        upper(hex(randomblob(4))) || '-' ||
                        upper(hex(randomblob(2))) || '-' ||
                        '4' || substr(upper(hex(randomblob(2))), 2) || '-' ||
                        substr('89AB', abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))), 2) || '-' ||
                        upper(hex(randomblob(6))),
                        'Module',
                        r.CraftMinutesId,
                        r.SkillId
                    FROM Recipe r;
            ");

            migrationBuilder.Sql(@"
                UPDATE Recipe SET LaborId = upper(hex(randomblob(4))) || '-' ||
                                            upper(hex(randomblob(2))) || '-' ||
                                            '4' || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                            substr('89AB', abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))), 2) || '-' ||
                                            upper(hex(randomblob(6))) WHERE true;

                INSERT INTO DynamicValue (Id, BaseValue, ServerId)
                    SELECT r.LaborId, r.Labor, r.ServerId FROM Recipe r;

                INSERT INTO Modifier (Id, DynamicType, DynamicValueId, SkillId)
                SELECT
                    upper(hex(randomblob(4))) || '-' ||
                    upper(hex(randomblob(2))) || '-' ||
                    '4' || substr(upper(hex(randomblob(2))), 2) || '-' ||
                    substr('89AB', abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))), 2) || '-' ||
                    upper(hex(randomblob(6))),
                    'Skill',
                    r.LaborId,
                    r.SkillId
                FROM Recipe r;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
