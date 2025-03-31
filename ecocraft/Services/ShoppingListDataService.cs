using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services
{
    public class ShoppingListDataService(
        ContextService contextService,
        DataContextDbService dataContextDbService,
        UserRecipeDbService userRecipeDbService,
        UserCraftingTableDbService userCraftingTableDbService,
        UserSkillDbService userSkillDbService,
        LocalizationService localizationService)
    {
        public DataContext CreateShoppingList(UserServer userServer)
        {
            var shoppingList = new DataContext
            {
                Name = localizationService.GetTranslation("ShoppingList.NewShoppingList") + (contextService.CurrentUserServer!.DataContexts.Count(d => d.IsShoppingList) + 1),
                UserServer = userServer,
                IsShoppingList = true,
            };

            contextService.CurrentUserServer!.DataContexts.Add(shoppingList);
            dataContextDbService.Add(shoppingList);

            return shoppingList;
        }

        public void RemoveShoppingList(DataContext shoppingList)
        {
            contextService.CurrentUserServer!.DataContexts.Remove(shoppingList);
            dataContextDbService.Delete(shoppingList);
        }

        public void AddUserRecipe(DataContext shoppingList, Recipe recipe, UserRecipe? parent = null, int quantityToCraft = 1)
        {
            var userRecipe = new UserRecipe
            {
                Recipe = recipe,
                DataContext = shoppingList,
                RoundFactor = quantityToCraft,
                ParentUserRecipe = parent,
            };

            shoppingList.UserRecipes.Add(userRecipe);
            userRecipeDbService.Add(userRecipe);

            if (recipe.CraftingTable.GetCurrentUserCraftingTable(shoppingList) is null)
            {
                GetOrCreateUserCraftingTable(shoppingList, recipe.CraftingTable);
            }

            if (recipe.Skill is not null && recipe.Skill.GetCurrentUserSkill(shoppingList) is null)
            {
                GetOrCreateUserSkill(shoppingList, recipe.Skill);
            }
        }

        public void RemoveUserRecipe(DataContext shoppingList, UserRecipe shoppingListRecipe)
        {
            var currentUserCraftingTable = shoppingListRecipe.Recipe.CraftingTable.GetCurrentUserCraftingTable(shoppingList);
            var currentUserSkill = shoppingListRecipe.Recipe.Skill?.GetCurrentUserSkill(shoppingList);

            shoppingList.UserRecipes.Remove(shoppingListRecipe);
            shoppingListRecipe.Recipe.UserRecipes.Remove(shoppingListRecipe);
            shoppingListRecipe.ParentUserRecipe?.ChildrenUserRecipes.Remove(shoppingListRecipe);

            foreach (var recipe in shoppingListRecipe.ChildrenUserRecipes.ToList())
            {
                RemoveUserRecipe(shoppingList, recipe);
            }

            userRecipeDbService.Delete(shoppingListRecipe);

            if (currentUserCraftingTable is not null && currentUserCraftingTable.CraftingTable.Recipes.All(r => r.GetCurrentUserRecipe(shoppingList) is null))
            {
                RemoveUserCraftingTable(shoppingList, currentUserCraftingTable);
            }

            if (currentUserSkill is not null && currentUserSkill.Skill!.Recipes.All(r => r.GetCurrentUserRecipe(shoppingList) is null))
            {
                RemoveUserSkill(shoppingList, currentUserSkill);
            }
        }

        private UserCraftingTable GetOrCreateUserCraftingTable(DataContext shoppingList, CraftingTable craftingTable)
        {
            var shoppingListCraftingTable = shoppingList.UserCraftingTables.Find(slct => slct.CraftingTable == craftingTable);

            if (shoppingListCraftingTable is not null)
            {
                return shoppingListCraftingTable;
            }

            var userCraftingTable = new UserCraftingTable
            {
                CraftingTable = craftingTable,
                PluginModule = craftingTable.GetCurrentUserCraftingTable(shoppingList)?.PluginModule,
                DataContext = shoppingList,
            };

            craftingTable.UserCraftingTables.Add(userCraftingTable);
            userCraftingTableDbService.Add(userCraftingTable);

            return userCraftingTable;
        }

        private void RemoveUserCraftingTable(DataContext shoppingList, UserCraftingTable userCraftingTable)
        {
            shoppingList.UserCraftingTables.Remove(userCraftingTable);
            userCraftingTableDbService.Delete(userCraftingTable);
        }

        private UserSkill GetOrCreateUserSkill(DataContext shoppingList, Skill skill)
        {
            var shoppingListSkill = shoppingList.UserSkills.Find(sls => sls.Skill == skill);

            if (shoppingListSkill is not null)
            {
                return shoppingListSkill;
            }

            var userSkill = new UserSkill
            {
                Skill = skill,
                Level = skill.GetCurrentUserSkill(shoppingList)?.Level ?? 0,
                DataContext = shoppingList,
            };

            skill.UserSkills.Add(userSkill);
            userSkillDbService.Add(userSkill);

            return userSkill;
        }

        private void RemoveUserSkill(DataContext shoppingList, UserSkill userSkill)
        {
            shoppingList.UserSkills.Remove(userSkill);
            userSkillDbService.Delete(userSkill);
        }
    }
}
