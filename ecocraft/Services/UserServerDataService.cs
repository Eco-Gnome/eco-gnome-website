using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services;

public class UserServerDataService(
    UserSkillDbService userSkillDbService,
    UserCraftingTableDbService userCraftingTableDbService,
    UserSettingDbService userSettingDbService,
    UserElementDbService userElementDbService,
    UserPriceDbService userPriceDbService,
    UserRecipeDbService userRecipeDbService,
    UserMarginDbService userMarginDbService,
    UserTalentDbService userTalentDbService,
    ServerDataService serverDataService,
    LocalizationService localizationService)
{
    public List<UserSkill> UserSkills { get; private set; } = [];
    public List<UserTalent> UserTalents { get; private set; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; private set; } = [];
    public UserSetting? UserSetting { get; private set; }
    public List<UserElement> UserElements { get; private set; } = [];
    public List<UserPrice> UserPrices { get; private set; } = [];
    public List<UserRecipe> UserRecipes { get; private set; } = [];
    public List<UserMargin> UserMargins { get; private set; } = [];

    public async Task RetrieveUserData(UserServer? userServer, DataContext? dataContext)
    {
        if (userServer is null)
        {
            UserSkills = [];
            UserTalents = [];
            UserCraftingTables = [];
            UserSetting = null;
            UserElements = [];
            UserPrices = [];
            UserRecipes = [];
            UserMargins = [];

            return;
        }

        dataContext ??= userServer.DataContexts.First(d => d.IsDefault);

        var userSkillsTask = userSkillDbService.GetByDataContextAsync(dataContext);
        var userTalentsTask = userTalentDbService.GetByDataContextAsync(dataContext);
        var userCraftingTablesTask = userCraftingTableDbService.GetByDataContextAsync(dataContext);
        var userSettingsTask = userSettingDbService.GetByDataContextAsync(dataContext);
        var userElementsTask = userElementDbService.GetByDataContextAsync(dataContext);
        var userPricesTask = userPriceDbService.GetByDataContextAsync(dataContext);
        var userRecipesTask = userRecipeDbService.GetByDataContextAsync(dataContext);
        var userMarginsTask = userMarginDbService.GetByDataContextAsync(dataContext);

        await Task.WhenAll(userSkillsTask, userTalentsTask, userCraftingTablesTask, userSettingsTask, userElementsTask, userPricesTask,
            userRecipesTask, userMarginsTask);

        UserSkills = userSkillsTask.Result;
        UserTalents = userTalentsTask.Result;
        UserCraftingTables = userCraftingTablesTask.Result;
        UserSetting = userSettingsTask.Result;
        UserElements = userElementsTask.Result;
        UserPrices = userPricesTask.Result;
        UserRecipes = userRecipesTask.Result;
        UserMargins = userMarginsTask.Result;

        foreach (var skill in serverDataService.Skills)
        {
            skill.CurrentUserSkill = UserSkills.FirstOrDefault(x => x.Skill == skill);

            foreach (var talent in skill.Talents)
            {
                talent.CurrentUserTalent = UserTalents.FirstOrDefault(x => x.Talent == talent);
            }
        }


        foreach (var craftingTable in serverDataService.CraftingTables)
        {
            craftingTable.CurrentUserCraftingTable = UserCraftingTables.FirstOrDefault(x => x.CraftingTable == craftingTable);
        }

        foreach (var recipe in serverDataService.Recipes)
        {
            recipe.CurrentUserRecipe = UserRecipes.FirstOrDefault(x => x.Recipe == recipe);

            foreach (var element in recipe.Elements)
            {
                element.CurrentUserElement = UserElements.FirstOrDefault(x => x.Element == element);
            }
        }

        foreach (var itemOrTag in serverDataService.ItemOrTags)
        {
            itemOrTag.CurrentUserPrice = UserPrices.FirstOrDefault(x => x.ItemOrTag == itemOrTag);
        }
    }

    public void AddUserSkill(Skill? skill, DataContext dataContext, bool onlyLevelAccessibleRecipes, bool addRecipes = true)
    {
        var userSkill = new UserSkill
        {
            Skill = skill,
            DataContext = dataContext,
            Level = 1,
        };

        UserSkills.Add(userSkill);
        userSkillDbService.Add(userSkill);

        if (!addRecipes) return;

        // Add related recipes
        var recipes = userSkill.Skill!.Recipes;

        if (onlyLevelAccessibleRecipes)
        {
            recipes = recipes.Where(r => r.SkillLevel <= userSkill.Level).ToList();
        }

        foreach (var recipe in recipes)
        {
            AddUserRecipe(recipe, dataContext);
        }
    }

    public void RemoveUserSkill(UserSkill userSkill)
    {
        // Remove related recipes
        foreach (var userRecipe in UserRecipes.Where(ur => ur.Recipe.Skill == userSkill.Skill).ToList())
        {
            RemoveUserRecipe(userRecipe);
        }

        userSkill.DataContext.UserSkills.Remove(userSkill);
        UserSkills.Remove(userSkill);
        userSkillDbService.Delete(userSkill);
    }

    public void AddUserTalent(Talent talent, DataContext dataContext)
    {
        var userTalent = new UserTalent
        {
            Talent = talent,
            DataContext = dataContext,
        };

        userTalentDbService.Add(userTalent);
        UserTalents.Add(userTalent);
    }

    public void RemoveUserTalent(UserTalent userTalent)
    {
        userTalentDbService.Delete(userTalent);
        UserTalents.Remove(userTalent);
    }

    public void CreateUserMargin(DataContext dataContext)
    {
        var userMargin = new UserMargin
        {
            Name = localizationService.GetTranslation("UserServerDataService.NewMargin"),
            DataContext = dataContext,
            Margin = 0,
        };

        UserMargins.Add(userMargin);
        userMarginDbService.Add(userMargin);
    }

    public void RemoveUserMargin(UserMargin userMargin)
    {
        UserMargins.Remove(userMargin);
        foreach (var userPrice in UserPrices.Where(up => up.UserMargin == userMargin))
        {
            userPrice.UserMargin = UserMargins.First();
        }
        userMarginDbService.Delete(userMargin);
    }

    public void UserSkillLevelChange(UserSkill userSkill, DataContext dataContext, bool isIncrease)
    {
        if (isIncrease)
        {
            // Get all recipes that should now be added, but not the existing ones
            foreach (var recipe in serverDataService.Recipes.Where(r =>
                         r.Skill == userSkill.Skill && r.SkillLevel <= userSkill.Level &&
                         !UserRecipes.Select(ur => ur.Recipe).Contains(r)).ToList())
            {
                AddUserRecipe(recipe, dataContext);
            }
        }
        else
        {
            // Get all recipes that should now be removed
            foreach (var userRecipe in UserRecipes
                         .Where(ur => ur.Recipe.Skill == userSkill.Skill && ur.Recipe.SkillLevel > userSkill.Level)
                         .ToList())
            {
                RemoveUserRecipe(userRecipe);
            }
        }
    }

    public void RecalculateUserRecipes(DataContext dataContext)
    {
        // We remove all recipes that does not meet the requirements
        foreach (var userRecipe in UserRecipes.ToList())
        {
            if (userRecipe.Recipe.Skill is not null && userRecipe.Recipe.SkillLevel >
                UserSkills.First(us => us.Skill == userRecipe.Recipe.Skill).Level)
            {
                RemoveUserRecipe(userRecipe);
            }
        }

        var selectedRecipes = UserRecipes.Select(ur => ur.Recipe);
        var onlyLevelAccessible = UserSetting!.OnlyLevelAccessibleRecipes;

        // We add all recipes that does meet the requirements
        foreach (var userSkill in UserSkills.ToList())
        {
            var recipesToAdd = userSkill.Skill?.Recipes ?? [];

            if (onlyLevelAccessible)
            {
                recipesToAdd = recipesToAdd.Where(r => r.SkillLevel <= userSkill.Level).ToList();
            }

            recipesToAdd = recipesToAdd.Where(r => !selectedRecipes.Contains(r)).ToList();

            foreach (var recipe in recipesToAdd)
            {
                AddUserRecipe(recipe, dataContext);
            }
        }
    }

    public void ToggleEmptyUserSkill(DataContext dataContext, bool displayNonSkilledRecipes)
    {
        var nullUserSkill = UserSkills.FirstOrDefault(us => us.Skill is null);

        if (displayNonSkilledRecipes)
        {
            if (nullUserSkill is null)
            {
                // Add a fake UserSkill without skill, so we can retrieve recipes that doesn't require skill easily
                AddUserSkill(null, dataContext, false, false);
            }
        }
        else
        {
            if (nullUserSkill is not null)
            {
                RemoveUserSkill(nullUserSkill);
            }
        }
    }

    public void AddUserCraftingTable(CraftingTable craftingTable, DataContext dataContext, bool addedByUser = false)
    {
        var userCraftingTable = new UserCraftingTable
        {
            CraftingTable = craftingTable,
            DataContext = dataContext,
            PluginModule = null
        };

        UserCraftingTables.Add(userCraftingTable);
        userCraftingTableDbService.Add(userCraftingTable);

        // If the crafting table is added by user, we add all recipes related to the crafting table and to skills
        if (addedByUser)
        {
            foreach (var recipe in serverDataService.Recipes.Where(r =>
                             UserSkills.Select(us => us.Skill).Contains(r.Skill) && r.CraftingTable == craftingTable)
                         .ToList())
            {
                AddUserRecipe(recipe, dataContext);
            }
        }
    }

    public void RemoveUserCraftingTable(UserCraftingTable userCraftingTable)
    {
        userCraftingTable.DataContext.UserCraftingTables.Remove(userCraftingTable);
        UserCraftingTables.Remove(userCraftingTable);
        userCraftingTableDbService.Delete(userCraftingTable);

        foreach (var userRecipe in UserRecipes.Where(ur => ur.Recipe.CraftingTable == userCraftingTable.CraftingTable)
                     .ToList())
        {
            RemoveUserRecipe(userRecipe, false);
        }
    }

    public void UpdateUserSetting(UserSetting userSetting)
    {
        userSettingDbService.Update(userSetting);
    }

    public void AddUserRecipe(Recipe recipe, DataContext dataContext)
    {
        var userRecipe = new UserRecipe
        {
            Recipe = recipe,
            DataContext = dataContext,
        };

        UserRecipes.Add(userRecipe);
        userRecipeDbService.Add(userRecipe);

        foreach (var element in recipe.Elements)
        {
            AddUserElement(element, dataContext);
        }

        if (!UserCraftingTables.Select(uct => uct.CraftingTable).Contains(recipe.CraftingTable))
        {
            AddUserCraftingTable(recipe.CraftingTable, dataContext);
        }
    }

    public void RemoveUserRecipe(UserRecipe userRecipe, bool removeCraftingTables = true)
    {
        foreach (var userElement in UserElements.Where(ue => ue.Element.Recipe == userRecipe.Recipe).ToList())
        {
            RemoveUserElement(userElement);
        }

        userRecipe.DataContext.UserRecipes.Remove(userRecipe);
        UserRecipes.Remove(userRecipe);
        userRecipeDbService.Delete(userRecipe);

        if (removeCraftingTables && UserRecipes.All(ur => ur.Recipe.CraftingTable != userRecipe.Recipe.CraftingTable))
        {
            RemoveUserCraftingTable(
                UserCraftingTables.First(uct => uct.CraftingTable == userRecipe.Recipe.CraftingTable));
        }
    }

    private void AddUserElement(Element element, DataContext dataContext)
    {
        var userElement = new UserElement
        {
            Element = element,
            DataContext = dataContext,
            Share = element.DefaultShare,
            IsReintegrated = element.DefaultIsReintegrated
        };

        UserElements.Add(userElement);
        userElementDbService.Add(userElement);

        // If the related ItemOrTag has no UserPrice, create it
        if (UserPrices.FirstOrDefault(up => up.ItemOrTag == userElement.Element.ItemOrTag) is null)
        {
            AddUserPrice(element.ItemOrTag, dataContext);

            if (element.ItemOrTag.IsTag)
            {
                foreach (var item in element.ItemOrTag.AssociatedItems)
                {
                    if (UserPrices.FirstOrDefault(up => up.ItemOrTag == item) is null)
                    {
                        AddUserPrice(item, dataContext);
                    }
                }
            }
        }
    }

    private void RemoveUserElement(UserElement userElement)
    {
        // Remove any existing PrimaryUserPrice
        foreach (var userPrice in UserPrices.Where(up => up.PrimaryUserElement == userElement))
        {
            userPrice.PrimaryUserElement = null;
        }

        userElement.DataContext.UserElements.Remove(userElement);
        UserElements.Remove(userElement);
        userElementDbService.Delete(userElement);

        // Remove the UserPrice if no other UserElement target it or no other tag.
        var otherUserElementsOfSameItemOrTag =
            UserElements.Where(u => u.Element.ItemOrTag == userElement.Element.ItemOrTag || u.Element.ItemOrTag.AssociatedItems.Contains(userElement.Element.ItemOrTag)).ToList();

        if (!otherUserElementsOfSameItemOrTag.Any())
        {
            var removedUserPrice = UserPrices.FirstOrDefault(up => up.ItemOrTag == userElement.Element.ItemOrTag);

            if (removedUserPrice is not null)
            {
                RemoveUserPrice(removedUserPrice);

                if (removedUserPrice.ItemOrTag.IsTag)
                {
                    foreach (var item in removedUserPrice.ItemOrTag.AssociatedItems)
                    {
                        var otherUserElementsOfSameItem =
                            UserElements.Where(u => u.Element.ItemOrTag == item || u.Element.ItemOrTag.AssociatedItems.Contains(item)).ToList();

                        if (!otherUserElementsOfSameItem.Any())
                        {
                            var itemUserPrice = UserPrices.FirstOrDefault(up => up.ItemOrTag == item);

                            if (itemUserPrice is not null)
                            {
                                RemoveUserPrice(itemUserPrice);
                            }
                        }
                    }
                }
            }
        }
    }

    private void AddUserPrice(ItemOrTag itemOrTag, DataContext dataContext)
    {
        var userPrice = new UserPrice
        {
            ItemOrTag = itemOrTag,
            DataContext = dataContext,
            UserMargin = UserMargins.First(),
            OverrideIsBought = false,
            Price = itemOrTag.DefaultPrice ?? itemOrTag.MinPrice,
        };

        UserPrices.Add(userPrice);
        userPriceDbService.Add(userPrice);
    }

    private void RemoveUserPrice(UserPrice userPrice)
    {
        userPrice.DataContext.UserPrices.Remove(userPrice);
        UserPrices.Remove(userPrice);
        userPriceDbService.Delete(userPrice);
    }

    public List<Recipe> GetAvailableRecipes()
    {
        var recipes = new HashSet<Recipe>();

        foreach (var userSkill in UserSkills)
        {
            var foundRecipes = serverDataService.Recipes.Where(r => r.Skill == userSkill.Skill);

            recipes.UnionWith(UserSetting!.OnlyLevelAccessibleRecipes
                ? foundRecipes.Where(r => r.SkillLevel <= userSkill.Level).ToList()
                : foundRecipes);
        }

        if (UserSetting!.DisplayNonSkilledRecipes)
        {
            recipes.UnionWith(UserRecipes.Select(ucr => ucr.Recipe)
                .Where(r => r.Skill is null).ToList());
        }

        return recipes.Where(r => !UserRecipes.Select(ur => ur.Recipe).Contains(r))
            .ToList();
    }

    public List<Skill> GetAvailableSkills()
    {
        var userSkills = UserSkills.Select(us => us.Skill);

        return serverDataService.Skills.Where(s => !userSkills.Contains(s)).ToList();
    }

    public List<CraftingTable> GetAvailableCraftingTables()
    {
        var userCraftingTables = UserCraftingTables.Select(uct => uct.CraftingTable);

        // Retrieve crafting tables that are not already used, and only crafting table that have a recipe with a skill defined in UserSkill
        return serverDataService.CraftingTables.Where(ct =>
            !userCraftingTables.Contains(ct) && ct.Recipes.Select(r => r.Skill)
                .Any(s => UserSkills.Select(us => us.Skill).Contains(s))).ToList();
    }
}
