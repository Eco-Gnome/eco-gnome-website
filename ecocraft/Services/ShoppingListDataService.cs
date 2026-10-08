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
            if (parent is null)
            {
                shoppingList.UserRecipes.Insert(0, userRecipe);
            }
            else
            {
                shoppingList.UserRecipes.Add(userRecipe);
            }
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
            RemoveUserRecipeInternal(context, shoppingList, shoppingListRecipe);
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

        // Writes the source context's skill levels, talents and table setups into the list rows it
        // also has (only the skills and tables the list's recipes use), then refreshes the sub-recipe
        // quantities. Only what differs is saved.
        public async Task ApplySourceDataContext(EcoCraftDbContext context, DataContext shoppingList, DataContext source)
        {
            foreach (var userSkill in shoppingList.UserSkills.Where(us => us.Skill is not null))
            {
                var sourceUserSkill = userSkill.Skill!.GetCurrentUserSkill(source);
                if (sourceUserSkill is null) continue;

                if (userSkill.Level != sourceUserSkill.Level)
                {
                    userSkill.Level = sourceUserSkill.Level;
                    userSkillDbService.UpdateLevel(context, userSkill);
                }

                CopyTalents(context, shoppingList, userSkill.Skill!, source);
            }

            foreach (var userCraftingTable in shoppingList.UserCraftingTables)
            {
                var sourceUserCraftingTable = userCraftingTable.CraftingTable.GetCurrentUserCraftingTable(source);
                if (sourceUserCraftingTable is null) continue;

                var pluginModules = userCraftingTable.CraftingTable.PluginModules
                    .Where(pm => sourceUserCraftingTable.PluginModules.Any(spm => spm.Id == pm.Id))
                    .ToList();

                if (pluginModules.Select(pm => pm.Id).ToHashSet().SetEquals(userCraftingTable.PluginModules.Select(pm => pm.Id))
                    && userCraftingTable.FuelItemId == sourceUserCraftingTable.FuelItemId
                    && userCraftingTable.AdditionalCraftMinuteFee == sourceUserCraftingTable.AdditionalCraftMinuteFee
                    && userCraftingTable.TotalCraftMinuteFee == sourceUserCraftingTable.TotalCraftMinuteFee)
                {
                    continue;
                }

                userCraftingTable.PluginModules = pluginModules;
                userCraftingTable.FuelItem = sourceUserCraftingTable.FuelItem;
                userCraftingTable.FuelItemId = sourceUserCraftingTable.FuelItemId;
                userCraftingTable.AdditionalCraftMinuteFee = sourceUserCraftingTable.AdditionalCraftMinuteFee;
                userCraftingTable.TotalCraftMinuteFee = sourceUserCraftingTable.TotalCraftMinuteFee;
                await userCraftingTableDbService.UpdateAllAsync(context, userCraftingTable);
            }

            SynchronizeRecipeTree(context, shoppingList);
        }

        private void CopyTalents(EcoCraftDbContext context, DataContext shoppingList, Skill skill, DataContext source)
        {
            foreach (var talent in skill.Talents)
            {
                var sourceUserTalent = talent.GetCurrentUserTalent(source);
                var userTalent = talent.GetCurrentUserTalent(shoppingList);

                if (sourceUserTalent is null && userTalent is not null)
                {
                    shoppingList.UserTalents.Remove(userTalent);
                    talent.UserTalents.Remove(userTalent);
                    userTalentDbService.Destroy(context, userTalent);
                }
                else if (sourceUserTalent is not null && userTalent is null)
                {
                    userTalent = new UserTalent
                    {
                        Talent = talent,
                        TalentId = talent.Id,
                        DataContext = shoppingList,
                        DataContextId = shoppingList.Id,
                        Level = sourceUserTalent.Level,
                    };
                    userTalentDbService.Create(context, userTalent);
                    shoppingList.UserTalents.Add(userTalent);
                    talent.UserTalents.Add(userTalent);
                }
                else if (sourceUserTalent is not null && userTalent!.Level != sourceUserTalent.Level)
                {
                    userTalent.Level = sourceUserTalent.Level;
                    userTalentDbService.UpdateLevel(context, userTalent);
                }
            }
        }

        private void RemoveUserRecipeInternal(EcoCraftDbContext context, DataContext shoppingList, UserRecipe shoppingListRecipe)
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
                RemoveUserRecipeInternal(context, shoppingList, recipe);
            }

            userRecipeDbService.Destroy(context, shoppingListRecipe);

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
            return ShoppingListCoverageCalculator.GetExpectedRoundFactor(parentRecipe, childRecipe, shoppingList);
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

            var fuelItem = sourceUserCraftingTable?.FuelItem;

            var userCraftingTable = new UserCraftingTable
            {
                CraftingTable = craftingTable,
                CraftingTableId = craftingTable.Id,
                FuelItem = fuelItem,
                FuelItemId = fuelItem?.Id,
                AdditionalCraftMinuteFee = sourceUserCraftingTable?.AdditionalCraftMinuteFee ?? 0m,
                TotalCraftMinuteFee = sourceUserCraftingTable?.TotalCraftMinuteFee ?? 0m,
                DataContext = shoppingList,
                DataContextId = shoppingList.Id,
            };

            userCraftingTableDbService.Create(context, userCraftingTable);
            shoppingList.UserCraftingTables.Add(userCraftingTable);
            craftingTable.UserCraftingTables.Add(userCraftingTable);

            if (sourceUserCraftingTable is not null && sourceUserCraftingTable.PluginModules.Count > 0)
            {
                userCraftingTable.PluginModules = craftingTable.PluginModules
                    .Where(pm => sourceUserCraftingTable.PluginModules.Any(spm => spm.Id == pm.Id))
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

            if (sourceDataContext is not null)
            {
                CopyTalents(context, shoppingList, skill, sourceDataContext);
            }

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
