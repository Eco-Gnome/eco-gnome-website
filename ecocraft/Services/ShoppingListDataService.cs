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
        UserTalentDbService userTalentDbService,
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

        public async Task<UserRecipe> AddUserRecipe(EcoCraftDbContext context, DataContext shoppingList, Recipe recipe, UserRecipe? parent = null, int quantityToCraft = 1, DataContext? sourceDataContext = null)
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
                await GetOrCreateUserCraftingTable(context, shoppingList, recipe.CraftingTable, sourceDataContext);
            }

            if (recipe.Skill is not null && recipe.Skill.GetCurrentUserSkill(shoppingList) is null)
            {
                GetOrCreateUserSkill(context, shoppingList, recipe.Skill, sourceDataContext);
            }

            return userRecipe;
        }

        public void RemoveUserRecipe(EcoCraftDbContext context, DataContext shoppingList, UserRecipe shoppingListRecipe)
        {
            RemoveUserRecipe(context, shoppingList, shoppingListRecipe, true);
        }

        public void SynchronizeRecipeTree(EcoCraftDbContext context, DataContext shoppingList)
        {
            foreach (var rootRecipe in shoppingList.GetRootShoppingListRecipes())
            {
                SynchronizeRecipeChildren(context, shoppingList, rootRecipe);
            }
        }

        public void SynchronizeRecipeSubtreeFrom(EcoCraftDbContext context, DataContext shoppingList, UserRecipe parentRecipe)
        {
            SynchronizeRecipeChildren(context, shoppingList, parentRecipe);
        }

        private void RemoveUserRecipe(EcoCraftDbContext context, DataContext shoppingList, UserRecipe shoppingListRecipe, bool destroyInDatabase)
        {
            var currentUserCraftingTableId = shoppingListRecipe.Recipe.CraftingTable.GetCurrentUserCraftingTable(shoppingList)?.Id;
            var currentUserSkillId = shoppingListRecipe.Recipe.Skill?.GetCurrentUserSkill(shoppingList)?.Id;
            var childrenRecipes = shoppingList.UserRecipes
                .Where(ur => ur.ParentUserRecipeId == shoppingListRecipe.Id)
                .ToList();

            shoppingList.UserRecipes.Remove(shoppingListRecipe);
            shoppingListRecipe.Recipe.UserRecipes.Remove(shoppingListRecipe);
            if (shoppingListRecipe.ParentUserRecipe is not null)
            {
                shoppingListRecipe.ParentUserRecipe.ChildrenUserRecipes.Remove(shoppingListRecipe);
            }
            else if (shoppingListRecipe.ParentUserRecipeId is Guid parentId)
            {
                shoppingList.UserRecipes.FirstOrDefault(ur => ur.Id == parentId)?.ChildrenUserRecipes.Remove(shoppingListRecipe);
            }

            foreach (var recipe in childrenRecipes)
            {
                RemoveUserRecipe(context, shoppingList, recipe, false);
            }

            if (destroyInDatabase)
            {
                userRecipeDbService.Destroy(context, shoppingListRecipe);
            }

            var currentUserCraftingTable = currentUserCraftingTableId is not null
                ? shoppingList.UserCraftingTables.FirstOrDefault(uct => uct.Id == currentUserCraftingTableId)
                : null;
            if (currentUserCraftingTable is not null && currentUserCraftingTable.CraftingTable.Recipes.All(r => r.GetCurrentUserRecipe(shoppingList) is null))
            {
                RemoveUserCraftingTable(context, shoppingList, currentUserCraftingTable);
            }

            var currentUserSkill = currentUserSkillId is not null
                ? shoppingList.UserSkills.FirstOrDefault(us => us.Id == currentUserSkillId)
                : null;
            if (currentUserSkill is not null && currentUserSkill.Skill!.Recipes.All(r => r.GetCurrentUserRecipe(shoppingList) is null))
            {
                RemoveUserSkill(context, shoppingList, currentUserSkill);
            }
        }

        private void SynchronizeRecipeChildren(EcoCraftDbContext context, DataContext shoppingList, UserRecipe parentRecipe)
        {
            foreach (var childRecipe in parentRecipe.ChildrenUserRecipes)
            {
                var expectedRoundFactor = GetExpectedRoundFactor(shoppingList, parentRecipe, childRecipe);

                if (childRecipe.RoundFactor != expectedRoundFactor)
                {
                    childRecipe.RoundFactor = expectedRoundFactor;
                    userRecipeDbService.UpdateRoundFactor(context, childRecipe);
                }

                SynchronizeRecipeChildren(context, shoppingList, childRecipe);
            }
        }

        private static int GetExpectedRoundFactor(DataContext shoppingList, UserRecipe parentRecipe, UserRecipe childRecipe)
        {
            var matchingIngredients = parentRecipe.Recipe.Elements
                .Where(e =>
                    e.IsIngredient() &&
                    childRecipe.Recipe.Elements.Any(product =>
                        product.IsProduct() &&
                        product.ItemOrTag.GetAssociatedTagsAndSelf().Any(iot => iot.Id == e.ItemOrTag.Id)))
                .ToList();

            if (matchingIngredients.Count == 0)
            {
                return Math.Max(childRecipe.RoundFactor, 1);
            }

            var requiredCrafts = matchingIngredients
                .Select(ingredient =>
                {
                    var requiredQuantity = Math.Abs(ingredient.Quantity.GetDynamicValue(shoppingList) * parentRecipe.RoundFactor);
                    var producedQuantity = childRecipe.Recipe.Elements
                        .Where(product =>
                            product.IsProduct() &&
                            product.ItemOrTag.GetAssociatedTagsAndSelf().Any(iot => iot.Id == ingredient.ItemOrTag.Id))
                        .Sum(product => product.Quantity.GetDynamicValue(shoppingList));

                    return producedQuantity <= 0 ? 1m : requiredQuantity / producedQuantity;
                })
                .DefaultIfEmpty(1m)
                .Max();

            return Math.Max((int)Math.Ceiling(requiredCrafts), 1);
        }

        private async Task<UserCraftingTable> GetOrCreateUserCraftingTable(EcoCraftDbContext context, DataContext shoppingList, CraftingTable craftingTable, DataContext? sourceDataContext)
        {
            var shoppingListCraftingTable = shoppingList.UserCraftingTables.Find(slct => slct.CraftingTableId == craftingTable.Id);

            if (shoppingListCraftingTable is not null)
            {
                return shoppingListCraftingTable;
            }

            var sourceUserCraftingTable = sourceDataContext is not null
                ? craftingTable.GetCurrentUserCraftingTable(sourceDataContext)
                : null;

            var pluginModule = sourceUserCraftingTable?.PluginModuleId is Guid pluginModuleId
                ? craftingTable.PluginModules.FirstOrDefault(pm => pm.Id == pluginModuleId)
                : null;

            var userCraftingTable = new UserCraftingTable
            {
                CraftingTable = craftingTable,
                CraftingTableId = craftingTable.Id,
                PluginModule = pluginModule,
                PluginModuleId = pluginModule?.Id,
                DataContext = shoppingList,
                DataContextId = shoppingList.Id,
            };

            userCraftingTableDbService.Create(context, userCraftingTable);
            shoppingList.UserCraftingTables.Add(userCraftingTable);
            craftingTable.UserCraftingTables.Add(userCraftingTable);

            if (sourceUserCraftingTable is not null && sourceUserCraftingTable.SkilledPluginModules.Count > 0)
            {
                userCraftingTable.SkilledPluginModules = craftingTable.PluginModules
                    .Where(pm => sourceUserCraftingTable.SkilledPluginModules.Any(spm => spm.Id == pm.Id))
                    .ToList();
                await userCraftingTableDbService.UpdateAllAsync(context, userCraftingTable);
            }

            return userCraftingTable;
        }

        private void RemoveUserCraftingTable(EcoCraftDbContext context, DataContext shoppingList, UserCraftingTable userCraftingTable)
        {
            shoppingList.UserCraftingTables.Remove(userCraftingTable);
            userCraftingTable.CraftingTable.UserCraftingTables.Remove(userCraftingTable);
            userCraftingTableDbService.Destroy(context, userCraftingTable);
        }

        private UserSkill GetOrCreateUserSkill(EcoCraftDbContext context, DataContext shoppingList, Skill skill, DataContext? sourceDataContext)
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
                Level = Math.Max(skill.GetCurrentUserSkill(sourceDataContext ?? shoppingList)?.Level ?? 1, 1),
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
            foreach (var userTalent in shoppingList.UserTalents.Where(ut => ut.Talent.SkillId == userSkill.SkillId).ToList())
            {
                shoppingList.UserTalents.Remove(userTalent);
                userTalent.Talent.UserTalents.Remove(userTalent);
                userTalentDbService.Destroy(context, userTalent);
            }

            shoppingList.UserSkills.Remove(userSkill);
            userSkill.Skill?.UserSkills.Remove(userSkill);
            userSkillDbService.Destroy(context, userSkill);
        }
    }
}
