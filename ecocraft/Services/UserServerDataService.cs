using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

public class UserServerDataService(
    UserSkillDbService userSkillDbService,
    UserRecipeDbService userRecipeDbService,
    UserCraftingTableDbService userCraftingTableDbService,
    UserTalentDbService userTalentDbService,
    UserPriceDbService userPriceDbService,
    UserElementDbService userElementDbService,
    UserMarginDbService userMarginDbService,
    LocalizationService localizationService)
{
    public void AddUserSkill(EcoCraftDbContext context, DataContext dataContext, Server server, Skill? skill, bool onlyLevelAccessibleRecipes, bool addRecipes = true)
    {
        var userSkill = new UserSkill
        {
            Skill = skill,
            SkillId = skill?.Id,
            DataContext = dataContext,
            DataContextId = dataContext.Id,
            Level = 1,
        };

        userSkillDbService.Create(context, userSkill);
        dataContext.UserSkills.Add(userSkill);
        skill?.UserSkills.Add(userSkill);

        if (!addRecipes) return;

        // Add related recipes
        var recipes = userSkill.Skill!.Recipes;

        if (onlyLevelAccessibleRecipes)
        {
            recipes = recipes.Where(r => r.SkillLevel <= userSkill.Level).ToList();
        }

        foreach (var recipe in recipes)
        {
            AddUserRecipe(context, dataContext, server, recipe);
        }
    }

    public void RemoveUserSkill(EcoCraftDbContext context, UserSkill userSkill)
    {
        // Remove related recipes
        foreach (var userRecipe in userSkill.DataContext.UserRecipes.Where(ur => ur.Recipe.Skill == userSkill.Skill).ToList())
        {
            RemoveUserRecipe(context, userRecipe);
        }

        // Remove UserTalents tied to this skill (no DB cascade: UserTalent FKs DataContext/Talent, not UserSkill)
        if (userSkill.Skill is not null)
        {
            foreach (var userTalent in userSkill.DataContext.UserTalents.Where(ut => ut.Talent.SkillId == userSkill.Skill.Id).ToList())
            {
                RemoveUserTalent(context, userTalent);
            }
        }

        userSkillDbService.Destroy(context, userSkill);
        userSkill.DataContext.UserSkills.Remove(userSkill);
        userSkill.Skill?.UserSkills.Remove(userSkill);
    }

    public void AddUserTalent(EcoCraftDbContext context, Talent talent, DataContext dataContext)
    {
        var userTalent = new UserTalent
        {
            Talent = talent,
            TalentId = talent.Id,
            DataContext = dataContext,
            DataContextId = dataContext.Id,
            Level = 1,
        };

        userTalentDbService.Create(context, userTalent);
        dataContext.UserTalents.Add(userTalent);
        talent.UserTalents.Add(userTalent);
    }

    public void RemoveUserTalent(EcoCraftDbContext context, UserTalent userTalent)
    {
        userTalentDbService.Destroy(context, userTalent);
        userTalent.DataContext.UserTalents.Remove(userTalent);
        userTalent.Talent.UserTalents.Remove(userTalent);
    }

    public void CreateUserMargin(EcoCraftDbContext context, DataContext dataContext)
    {
        var userMargin = new UserMargin
        {
            Name = localizationService.GetTranslation("UserServerDataService.NewMargin"),
            DataContext = dataContext,
            DataContextId = dataContext.Id,
            Margin = 0,
        };

        userMarginDbService.Create(context, userMargin);
        dataContext.UserMargins.Add(userMargin);
    }

    public void RemoveUserMargin(EcoCraftDbContext context, UserMargin userMargin)
    {
        var replacementMargin = userMargin.DataContext.UserMargins.First();

        foreach (var userPrice in userMargin.DataContext.UserPrices.Where(up => up.UserMargin == userMargin))
        {
            userPrice.UserMargin = replacementMargin;
            userPriceDbService.UpdateAll(context, userPrice);
            replacementMargin.UserPrices.Add(userPrice);
        }

        userMarginDbService.Destroy(context, userMargin);
        userMargin.DataContext.UserMargins.Remove(userMargin);
    }

    public void UserSkillLevelChange(EcoCraftDbContext context, DataContext dataContext, Server server, UserSkill userSkill, bool isIncrease)
    {
        if (isIncrease)
        {
            // Get all recipes that should now be added, but not the existing ones
            foreach (var recipe in server.Recipes.Where(r =>
                         r.Skill == userSkill.Skill && r.SkillLevel <= userSkill.Level &&
                         !dataContext.UserRecipes.Select(ur => ur.Recipe).Contains(r)).ToList())
            {
                AddUserRecipe(context, dataContext, server, recipe);
            }
        }
        else
        {
            // Get all recipes that should now be removed
            foreach (var userRecipe in dataContext.UserRecipes
                         .Where(ur => ur.Recipe.Skill == userSkill.Skill && ur.Recipe.SkillLevel > userSkill.Level)
                         .ToList())
            {
                RemoveUserRecipe(context, userRecipe);
            }
        }
    }

    public void RecalculateUserRecipes(EcoCraftDbContext context, DataContext dataContext, Server server)
    {
        // We remove all recipes that does not meet the requirements
        foreach (var userRecipe in dataContext.UserRecipes.ToList())
        {
            if (userRecipe.Recipe.Skill is not null && userRecipe.Recipe.SkillLevel > dataContext.UserSkills.First(us => us.Skill == userRecipe.Recipe.Skill).Level)
            {
                RemoveUserRecipe(context, userRecipe);
            }
        }

        var selectedRecipes = dataContext.UserRecipes.Select(ur => ur.Recipe);
        var onlyLevelAccessible = dataContext.UserSettings.First().OnlyLevelAccessibleRecipes;

        // We add all recipes that does meet the requirements
        foreach (var userSkill in dataContext.UserSkills.ToList())
        {
            var recipesToAdd = userSkill.Skill?.Recipes ?? [];

            if (onlyLevelAccessible)
            {
                recipesToAdd = recipesToAdd.Where(r => r.SkillLevel <= userSkill.Level).ToList();
            }

            recipesToAdd = recipesToAdd.Where(r => !selectedRecipes.Contains(r)).ToList();

            foreach (var recipe in recipesToAdd)
            {
                AddUserRecipe(context, dataContext, server, recipe);
            }
        }
    }

    public void ToggleEmptyUserSkill(EcoCraftDbContext context, DataContext dataContext, Server server, bool displayNonSkilledRecipes)
    {
        var nullUserSkill = dataContext.UserSkills.FirstOrDefault(us => us.Skill is null);

        if (displayNonSkilledRecipes)
        {
            if (nullUserSkill is null)
            {
                // Add a fake UserSkill without skill, so we can retrieve recipes that doesn't require skill easily
                AddUserSkill(context, dataContext, server, null, false, false);
            }
        }
        else
        {
            if (nullUserSkill is not null)
            {
                RemoveUserSkill(context, nullUserSkill);
            }
        }
    }

    public void AddUserCraftingTable(EcoCraftDbContext context, DataContext dataContext, Server server, CraftingTable craftingTable, bool addedByUser = false)
    {
        var userCraftingTable = new UserCraftingTable
        {
            CraftingTable = craftingTable,
            CraftingTableId = craftingTable.Id,
            DataContext = dataContext,
            DataContextId = dataContext.Id,
            PluginModule = null
        };

        userCraftingTableDbService.Create(context, userCraftingTable);
        dataContext.UserCraftingTables.Add(userCraftingTable);
        craftingTable.UserCraftingTables.Add(userCraftingTable);

        // If the crafting table is added by user, we add all recipes related to the crafting table and to skills
        if (addedByUser)
        {
            foreach (var recipe in server.Recipes.Where(r => dataContext.UserSkills.Select(us => us.Skill).Contains(r.Skill) && r.CraftingTable == craftingTable).ToList())
            {
                AddUserRecipe(context, dataContext, server, recipe);
            }
        }
    }

    public void RemoveUserCraftingTable(EcoCraftDbContext context, DataContext dataContext, UserCraftingTable userCraftingTable)
    {
        userCraftingTableDbService.Destroy(context, userCraftingTable);
        dataContext.UserCraftingTables.Remove(userCraftingTable);
        userCraftingTable.CraftingTable.UserCraftingTables.Remove(userCraftingTable);

        foreach (var userRecipe in dataContext.UserRecipes.Where(ur => ur.Recipe.CraftingTable == userCraftingTable.CraftingTable).ToList())
        {
            RemoveUserRecipe(context, userRecipe, false);
        }
    }

    public void AddUserRecipe(EcoCraftDbContext context, DataContext dataContext, Server server, Recipe recipe)
    {
        var userRecipe = new UserRecipe
        {
            Recipe = recipe,
            RecipeId = recipe.Id,
            DataContext = dataContext,
            DataContextId = dataContext.Id,
        };

        userRecipeDbService.Create(context, userRecipe);
        dataContext.UserRecipes.Add(userRecipe);
        recipe.UserRecipes.Add(userRecipe);

        foreach (var element in recipe.Elements)
        {
            AddUserElementIfNotExists(context, element, userRecipe, dataContext);
        }

        if (!dataContext.UserCraftingTables.Select(uct => uct.CraftingTable).Contains(recipe.CraftingTable))
        {
            AddUserCraftingTable(context, dataContext, server, recipe.CraftingTable);
        }
    }

    public void RemoveUserRecipe(EcoCraftDbContext context, UserRecipe userRecipe, bool removeCraftingTables = true)
    {
        foreach (var userElement in userRecipe.UserElements.ToList())
        {
            RemoveUserElement(context, userElement);
        }

        userRecipeDbService.Destroy(context, userRecipe);
        userRecipe.DataContext.UserRecipes.Remove(userRecipe);
        userRecipe.Recipe.UserRecipes.Remove(userRecipe);

        if (removeCraftingTables && userRecipe.DataContext.UserRecipes.All(ur => ur.Recipe.CraftingTable != userRecipe.Recipe.CraftingTable))
        {
            var orphanUserCraftingTable = userRecipe.DataContext.UserCraftingTables.FirstOrDefault(uct => uct.CraftingTable == userRecipe.Recipe.CraftingTable);
            if (orphanUserCraftingTable is not null)
            {
                RemoveUserCraftingTable(context, userRecipe.DataContext, orphanUserCraftingTable);
            }
        }
    }

    public void AddUserElementIfNotExists(EcoCraftDbContext context, Element element, UserRecipe userRecipe, DataContext dataContext)
    {
        if (element.GetCurrentUserElement(dataContext) is null)
        {
            var userElement = new UserElement
            {
                Element = element,
                ElementId = element.Id,
                DataContext = dataContext,
                DataContextId = dataContext.Id,
                Share = element.DefaultShare,
                IsReintegrated = element.DefaultIsReintegrated,
                UserRecipe = userRecipe,
                UserRecipeId = userRecipe.Id
            };

            userElementDbService.Create(context, userElement);
            element.UserElements.Add(userElement);
            dataContext.UserElements.Add(userElement);
            userRecipe.UserElements.Add(userElement);
        }

        foreach (var itemOrTag in element.ItemOrTag.GetAssociatedItemsAndSelf())
        {
            if (itemOrTag.GetCurrentUserPrice(dataContext) is null)
            {
                AddUserPrice(context, dataContext, itemOrTag);
            }
        }
    }

    private void RemoveUserElement(EcoCraftDbContext context, UserElement userElement)
    {
        // Clear any UserPrice pointing to this UserElement as its primary element first, so the
        // DB-level UserPrice → PrimaryUserElement cascade doesn't fire when the UserElement
        // row disappears.
        foreach (var userPrice in userElement.UserPricesPrimaryOf)
        {
            userPrice.PrimaryUserElement = null;
            userPriceDbService.UpdateAll(context, userPrice);
        }

        var itemOrTagAssociated = userElement.Element.ItemOrTag;

        userElementDbService.Destroy(context, userElement);
        userElement.Element.UserElements.Remove(userElement);
        userElement.UserRecipe.UserElements.Remove(userElement);
        userElement.DataContext.UserElements.Remove(userElement);

        // Remove the UserPrice of the related itemOrTag, and it's associated items, if no other related Elements have a UserElement
        if (itemOrTagAssociated.GetAssociatedTagsAndSelf().SelectMany(i => i.Elements).All(e => e.GetCurrentUserElement(userElement.DataContext) is null))
        {
            var itemOrTagAssociatedUserPrice = itemOrTagAssociated.GetCurrentUserPrice(userElement.DataContext);

            if (itemOrTagAssociatedUserPrice is not null)
            {
                RemoveUserPrice(context, itemOrTagAssociatedUserPrice);

                foreach (var itemOrTag in itemOrTagAssociated.AssociatedItems)
                {
                    if (itemOrTag.GetAssociatedTagsAndSelf().SelectMany(i => i.Elements).All(e => e.GetCurrentUserElement(userElement.DataContext) is null))
                    {
                        var itemOrTagUserPrice = itemOrTag.GetCurrentUserPrice(userElement.DataContext);

                        if (itemOrTagUserPrice is not null)
                        {
                            RemoveUserPrice(context, itemOrTagUserPrice);
                        }
                    }
                }
            }
        }
    }

    private void AddUserPrice(EcoCraftDbContext context, DataContext dataContext, ItemOrTag itemOrTag)
    {
        var userMargin = dataContext.UserMargins.First();

        var userPrice = new UserPrice
        {
            ItemOrTag = itemOrTag,
            ItemOrTagId = itemOrTag.Id,
            DataContext = dataContext,
            DataContextId = dataContext.Id,
            UserMargin = userMargin,
            UserMarginId = userMargin.Id,
            OverrideIsBought = false,
            Price = itemOrTag.DefaultPrice ?? itemOrTag.MinPrice,
        };

        userPriceDbService.Create(context, userPrice);
        dataContext.UserPrices.Add(userPrice);
        itemOrTag.UserPrices.Add(userPrice);
        userMargin.UserPrices.Add(userPrice);
    }

    private void RemoveUserPrice(EcoCraftDbContext context, UserPrice userPrice)
    {
        userPriceDbService.Destroy(context, userPrice);
        userPrice.DataContext.UserPrices.Remove(userPrice);
        userPrice.ItemOrTag.UserPrices.Remove(userPrice);
        userPrice.UserMargin?.UserPrices.Remove(userPrice);
    }

    public List<Recipe> GetAvailableRecipes(DataContext dataContext, Server server)
    {
        var recipes = new HashSet<Recipe>();

        foreach (var userSkill in dataContext.UserSkills)
        {
            var foundRecipes = server.Recipes.Where(r => r.Skill == userSkill.Skill);

            recipes.UnionWith(dataContext.UserSettings.First().OnlyLevelAccessibleRecipes
                ? foundRecipes.Where(r => r.SkillLevel <= userSkill.Level).ToList()
                : foundRecipes);
        }

        if (dataContext.UserSettings.First().DisplayNonSkilledRecipes)
        {
            recipes.UnionWith(dataContext.UserRecipes.Select(ucr => ucr.Recipe).Where(r => r.Skill is null).ToList());
        }

        return recipes
            .Where(r => !dataContext.UserRecipes.Select(ur => ur.Recipe).Contains(r))
            .ToList();
    }

    public List<Skill> GetAvailableSkills(DataContext dataContext, Server server)
    {
        var userSkills = dataContext.UserSkills.Select(us => us.Skill);

        return server.Skills.Where(s => !userSkills.Contains(s)).ToList();
    }

    public List<CraftingTable> GetAvailableCraftingTables(DataContext dataContext, Server server)
    {
        var userCraftingTables = dataContext.UserCraftingTables.Select(uct => uct.CraftingTable);

        // Retrieve crafting tables that are not already used, and only crafting table that have a recipe with a skill defined in UserSkill
        return server.CraftingTables
            .Where(ct => !userCraftingTables.Contains(ct) && ct.Recipes.Select(r => r.Skill).Any(s => dataContext.UserSkills.Select(us => us.Skill).Contains(s)))
            .ToList();
    }
}
