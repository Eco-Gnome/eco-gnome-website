namespace ecocraft.Models;

public class DynamicValueCalculationContext
{
    public IReadOnlyDictionary<Guid, UserSkill>? UserSkillsBySkillId { get; init; }
    public IReadOnlyDictionary<Guid, UserTalent>? UserTalentsByTalentId { get; init; }
    public IReadOnlyDictionary<Guid, UserCraftingTable>? UserCraftingTablesByCraftingTableId { get; init; }
    public IReadOnlyDictionary<Guid, UserRecipe>? UserRecipesByRecipeId { get; init; }
    public IDictionary<Guid, decimal>? DynamicValueCache { get; init; }
    public IDictionary<Guid, decimal>? RoundDynamicValueCache { get; init; }

    public UserSkill? GetUserSkill(Skill skill, DataContext dataContext)
    {
        if (UserSkillsBySkillId?.TryGetValue(skill.Id, out var userSkill) == true)
        {
            return userSkill;
        }

        return skill.GetCurrentUserSkill(dataContext);
    }

    public UserTalent? GetUserTalent(Guid talentId, DataContext dataContext)
    {
        if (UserTalentsByTalentId?.TryGetValue(talentId, out var userTalent) == true)
        {
            return userTalent;
        }

        return dataContext.UserTalents.FirstOrDefault(ut => ut.TalentId == talentId);
    }

    public UserCraftingTable? GetUserCraftingTable(Recipe recipe, DataContext dataContext)
    {
        if (UserCraftingTablesByCraftingTableId?.TryGetValue(recipe.CraftingTableId, out var userCraftingTable) == true)
        {
            return userCraftingTable;
        }

        return recipe.CraftingTable.GetCurrentUserCraftingTable(dataContext);
    }

    public int GetRoundFactor(Recipe recipe, DataContext dataContext)
    {
        if (UserRecipesByRecipeId?.TryGetValue(recipe.Id, out var userRecipe) == true)
        {
            return userRecipe.RoundFactor;
        }

        return recipe.GetCurrentUserRecipe(dataContext)?.RoundFactor ?? 0;
    }
}
