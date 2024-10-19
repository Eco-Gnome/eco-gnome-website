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
    ServerDataService serverDataService)
{
    public List<UserSkill> UserSkills { get; private set; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; private set; } = [];
    public UserSetting? UserSetting { get; private set; }
    public List<UserElement> UserElements { get; private set; } = [];
    public List<UserPrice> UserPrices { get; private set; } = [];
    public List<UserRecipe> UserRecipes { get; private set; } = [];

    public async Task RetrieveUserData(UserServer? userServer)
    {
        if (userServer is null)
        {
            UserSkills = [];
            UserCraftingTables = [];
            UserSetting = null;
            UserElements = [];
            UserPrices = [];
            UserRecipes = [];

            return;
        }

        var userSkillsTask = userSkillDbService.GetByUserServerAsync(userServer);
        var userCraftingTablesTask = userCraftingTableDbService.GetByUserServerAsync(userServer);
        var userSettingsTask = userSettingDbService.GetByUserServerAsync(userServer);
        var userElementsTask = userElementDbService.GetByUserServerAsync(userServer);
        var userPricesTask = userPriceDbService.GetByUserServerAsync(userServer);
        var userRecipesTask = userRecipeDbService.GetByUserServerAsync(userServer);

        await Task.WhenAll(userSkillsTask, userCraftingTablesTask, userSettingsTask, userElementsTask, userPricesTask,
            userRecipesTask);

        UserSkills = userSkillsTask.Result;
        UserCraftingTables = userCraftingTablesTask.Result;
        UserSetting = userSettingsTask.Result;
        UserElements = userElementsTask.Result;
        UserPrices = userPricesTask.Result;
        UserRecipes = userRecipesTask.Result;
    }

    public void AddUserSkill(Skill? skill, UserServer userServer, bool onlyLevelAccessibleRecipes, bool addRecipes = true)
    {
        var userSkill = new UserSkill
        {
            Skill = skill,
            UserServer = userServer,
            Level = 1,
        };

        UserSkills.Add(userSkill);
        userSkillDbService.Add(userSkill);

        if (addRecipes) {
            // Add related recipes
            var recipes = userSkill.Skill.Recipes;

            if (onlyLevelAccessibleRecipes)
            {
                recipes = recipes.Where(r => r.SkillLevel <= userSkill.Level).ToList();
            }

            foreach (var recipe in recipes)
            {
                AddUserRecipe(recipe, userServer);
            }
        }
    }

    public void RemoveUserSkill(UserSkill userSkill)
    {
        // Remove related recipes
        foreach (var userRecipe in UserRecipes.Where(ur => ur.Recipe.Skill == userSkill.Skill).ToList())
        {
            RemoveUserRecipe(userRecipe);
        }

        userSkill.UserServer.UserSkills.Remove(userSkill);
        UserSkills.Remove(userSkill);
        userSkillDbService.Delete(userSkill);
    }

    public void UserSkillLevelChange(UserSkill userSkill, UserServer userServer, bool isIncrease)
    {
        if (isIncrease)
        {
            // Get all recipes that should now be added, but not the existing ones
            foreach (var recipe in serverDataService.Recipes.Where(r =>
                         r.Skill == userSkill.Skill && r.SkillLevel <= userSkill.Level &&
                         !UserRecipes.Select(ur => ur.Recipe).Contains(r)).ToList())
            {
                AddUserRecipe(recipe, userServer);
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

    public void RemoveNonAllowedUserRecipes()
    {
        // If we activate the limitation of recipes, we remove all recipes that does not meet the requirements
        foreach (var userRecipe in UserRecipes.ToList())
        {
            if (userRecipe.Recipe.Skill is not null && userRecipe.Recipe.SkillLevel >
                UserSkills.First(us => us.Skill == userRecipe.Recipe.Skill).Level)
            {
                RemoveUserRecipe(userRecipe);
            }
        }
    }

    public void ToggleEmptyUserSkill(UserServer userServer, bool displayNonSkilledRecipes)
    {
        var nullUserSkill = UserSkills.FirstOrDefault(us => us.Skill is null);

        if (displayNonSkilledRecipes)
        {
            if (nullUserSkill is null)
            {
                // Add a fake UserSkill without skill, so we can retrieve recipes that doesn't require skill easily
                AddUserSkill(null, userServer, false, false);
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

    public void AddUserCraftingTable(CraftingTable craftingTable, UserServer userServer, bool addedByUser = false)
    {
        var userCraftingTable = new UserCraftingTable
        {
            CraftingTable = craftingTable,
            UserServer = userServer,
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
                AddUserRecipe(recipe, userServer);
            }
        }
    }

    public void RemoveUserCraftingTable(UserCraftingTable userCraftingTable)
    {
        userCraftingTable.UserServer.UserCraftingTables.Remove(userCraftingTable);
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

    public void AddUserRecipe(Recipe recipe, UserServer userServer)
    {
        var userRecipe = new UserRecipe
        {
            Recipe = recipe,
            UserServer = userServer,
        };

        UserRecipes.Add(userRecipe);
        userRecipeDbService.Add(userRecipe);

        foreach (var element in recipe.Elements)
        {
            AddUserElement(element, userServer);
        }

        if (!UserCraftingTables.Select(uct => uct.CraftingTable).Contains(recipe.CraftingTable))
        {
            AddUserCraftingTable(recipe.CraftingTable, userServer);
        }
    }

    public void RemoveUserRecipe(UserRecipe userRecipe, bool removeCraftingTables = true)
    {
        foreach (var userElement in UserElements.Where(ue => ue.Element.Recipe == userRecipe.Recipe).ToList())
        {
            RemoveUserElement(userElement);
        }

        userRecipe.UserServer.UserRecipes.Remove(userRecipe);
        UserRecipes.Remove(userRecipe);
        userRecipeDbService.Delete(userRecipe);

        if (removeCraftingTables && UserRecipes.All(ur => ur.Recipe.CraftingTable != userRecipe.Recipe.CraftingTable))
        {
            RemoveUserCraftingTable(
                UserCraftingTables.First(uct => uct.CraftingTable == userRecipe.Recipe.CraftingTable));
        }
    }

    private void AddUserElement(Element element, UserServer userServer)
    {
        var userElement = new UserElement
        {
            Element = element,
            UserServer = userServer,
            Share = element.IsIngredient() ? -1 : element.Index == 0 ? 1 : 0,
        };

        UserElements.Add(userElement);
        userElementDbService.Add(userElement);

        // If the related ItemOrTag has no UserPrice, create it
        if (UserPrices.FirstOrDefault(up => up.ItemOrTag == userElement.Element.ItemOrTag) is null)
        {
            AddUserPrice(element.ItemOrTag, userServer);

            if (element.ItemOrTag.IsTag)
            {
                foreach (var item in element.ItemOrTag.AssociatedItems)
                {
                    if (UserPrices.FirstOrDefault(up => up.ItemOrTag == item) is null)
                    {
                        AddUserPrice(item, userServer);
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

        userElement.UserServer.UserElements.Remove(userElement);
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

    private void AddUserPrice(ItemOrTag itemOrTag, UserServer userServer)
    {
        var userPrice = new UserPrice
        {
            ItemOrTag = itemOrTag,
            UserServer = userServer,
        };

        UserPrices.Add(userPrice);
        userPriceDbService.Add(userPrice);
    }

    private void RemoveUserPrice(UserPrice userPrice)
    {
        userPrice.UserServer.UserPrices.Remove(userPrice);
        UserPrices.Remove(userPrice);
        userPriceDbService.Delete(userPrice);
    }

    public List<Recipe> GetAvailableRecipes(bool limitToSkillLevelRecipes = false, bool addNonSkilledRecipes = false)
    {
        var recipes = new HashSet<Recipe>();

        foreach (var userSkill in UserSkills)
        {
            var foundRecipes = serverDataService.Recipes.Where(r => r.Skill == userSkill.Skill);

            recipes.UnionWith(limitToSkillLevelRecipes
                ? foundRecipes.Where(r => r.SkillLevel <= userSkill.Level).ToList()
                : foundRecipes);
        }

        if (addNonSkilledRecipes)
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
