using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
    public class ShoppingListDataService(
        IDbContextFactory<EcoCraftDbContext> factory,
        ContextService contextService,
        DataContextDbService dataContextDbService,
        UserRecipeDbService userRecipeDbService,
        UserCraftingTableDbService userCraftingTableDbService,
        UserSkillDbService userSkillDbService,
        LocalizationService localizationService)
    {
        public async Task<DataContext> CreateShoppingList(UserServer userServer)
        {
            var shoppingList = new DataContext
            {
                Name = localizationService.GetTranslation("ShoppingList.NewShoppingList") + (contextService.CurrentUserServer!.DataContexts.Count(d => d.IsShoppingList) + 1),
                UserServer = userServer,
                IsShoppingList = true,
            };

            await EcoCraftDbContext.ContextSaveAsync(factory, context =>
            {
                dataContextDbService.Create(context, shoppingList);
                return Task.CompletedTask;
            });

            contextService.CurrentUserServer!.DataContexts.Add(shoppingList);

            return shoppingList;
        }

        public void AddUserRecipe(EcoCraftDbContext context, DataContext shoppingList, Recipe recipe, UserRecipe? parent = null, int quantityToCraft = 1)
        {
            var userRecipe = new UserRecipe
            {
                Recipe = recipe,
                RecipeId = recipe.Id,
                DataContext = shoppingList,
                DataContextId = shoppingList.Id,
                RoundFactor = quantityToCraft,
                ParentUserRecipe = parent,
                ParentUserRecipeId = parent?.Id,
            };

            userRecipeDbService.Create(context, userRecipe);
            shoppingList.UserRecipes.Add(userRecipe);
            recipe.UserRecipes.Add(userRecipe);
            parent?.ChildrenUserRecipes.Add(userRecipe);

            if (recipe.CraftingTable.GetCurrentUserCraftingTable(shoppingList) is null)
            {
                GetOrCreateUserCraftingTable(context, shoppingList, recipe.CraftingTable);
            }

            if (recipe.Skill is not null && recipe.Skill.GetCurrentUserSkill(shoppingList) is null)
            {
                GetOrCreateUserSkill(context, shoppingList, recipe.Skill);
            }
        }

        public void RemoveUserRecipe(EcoCraftDbContext context, DataContext shoppingList, UserRecipe shoppingListRecipe)
        {
            var currentUserCraftingTable = shoppingListRecipe.Recipe.CraftingTable.GetCurrentUserCraftingTable(shoppingList);
            var currentUserSkill = shoppingListRecipe.Recipe.Skill?.GetCurrentUserSkill(shoppingList);

            shoppingList.UserRecipes.Remove(shoppingListRecipe);
            shoppingListRecipe.Recipe.UserRecipes.Remove(shoppingListRecipe);
            shoppingListRecipe.ParentUserRecipe?.ChildrenUserRecipes.Remove(shoppingListRecipe);

            foreach (var recipe in shoppingListRecipe.ChildrenUserRecipes.ToList())
            {
                RemoveUserRecipe(context, shoppingList, recipe);
            }

            userRecipeDbService.Destroy(context, shoppingListRecipe);

            if (currentUserCraftingTable is not null && currentUserCraftingTable.CraftingTable.Recipes.All(r => r.GetCurrentUserRecipe(shoppingList) is null))
            {
                RemoveUserCraftingTable(context, shoppingList, currentUserCraftingTable);
            }

            if (currentUserSkill is not null && currentUserSkill.Skill!.Recipes.All(r => r.GetCurrentUserRecipe(shoppingList) is null))
            {
                RemoveUserSkill(context, shoppingList, currentUserSkill);
            }
        }

        private UserCraftingTable GetOrCreateUserCraftingTable(EcoCraftDbContext context, DataContext shoppingList, CraftingTable craftingTable)
        {
            var shoppingListCraftingTable = shoppingList.UserCraftingTables.Find(slct => slct.CraftingTableId == craftingTable.Id);

            if (shoppingListCraftingTable is not null)
            {
                return shoppingListCraftingTable;
            }

            var userCraftingTable = new UserCraftingTable
            {
                CraftingTable = craftingTable,
                CraftingTableId = craftingTable.Id,
                PluginModule = craftingTable.GetCurrentUserCraftingTable(shoppingList)?.PluginModule,
                PluginModuleId = craftingTable.GetCurrentUserCraftingTable(shoppingList)?.PluginModule?.Id,
                DataContext = shoppingList,
                DataContextId = shoppingList.Id,
            };

            userCraftingTableDbService.Create(context, userCraftingTable);
            shoppingList.UserCraftingTables.Add(userCraftingTable);
            craftingTable.UserCraftingTables.Add(userCraftingTable);

            return userCraftingTable;
        }

        private void RemoveUserCraftingTable(EcoCraftDbContext context, DataContext shoppingList, UserCraftingTable userCraftingTable)
        {
            shoppingList.UserCraftingTables.Remove(userCraftingTable);
            userCraftingTable.CraftingTable.UserCraftingTables.Remove(userCraftingTable);
            userCraftingTableDbService.Destroy(context, userCraftingTable);
        }

        private UserSkill GetOrCreateUserSkill(EcoCraftDbContext context, DataContext shoppingList, Skill skill)
        {
            var shoppingListSkill = shoppingList.UserSkills.Find(sls => sls.SkillId == skill.Id);

            if (shoppingListSkill is not null)
            {
                return shoppingListSkill;
            }

            var userSkill = new UserSkill
            {
                Skill = skill,
                SkillId = skill.Id,
                Level = Math.Max(skill.GetCurrentUserSkill(shoppingList)?.Level ?? 1, 1),
                DataContext = shoppingList,
                DataContextId = shoppingList.Id,
            };

            userSkillDbService.Create(context, userSkill);
            shoppingList.UserSkills.Add(userSkill);
            skill.UserSkills.Add(userSkill);

            return userSkill;
        }

        private void RemoveUserSkill(EcoCraftDbContext context, DataContext shoppingList, UserSkill userSkill)
        {
            shoppingList.UserSkills.Remove(userSkill);
            userSkill.Skill?.UserSkills.Remove(userSkill);
            userSkillDbService.Destroy(context, userSkill);
        }
    }
}
