using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services
{
    public class ShoppingListDataService(
    ShoppingListDbService shoppingListDbService,
    ShoppingListRecipeDbService shoppingListRecipeDbService,
    ShoppingListItemOrTagDbService shoppingListItemOrTagDbService,
    ShoppingListCraftingTableDbService shoppingListCraftingTableDbService,
    ShoppingListSkillDbService shoppingListSkillDbService,
    UserServerDataService userServerDataService,
    LocalizationService localizationService)
    {
        public List<ShoppingList> ShoppingLists { get; private set; } = [];

        public async Task RetrieveShoppingLists(UserServer? userServer)
        {
            if (userServer is null)
            {
                ShoppingLists = [];

                return;
            }

            var userShoppingListsTask = shoppingListDbService.GetByUserServerAsync(userServer);

            await Task.WhenAll(userShoppingListsTask);

            ShoppingLists = userShoppingListsTask.Result;
        }

        public ShoppingList CreateShoppingList(UserServer userServer)
        {
            var shoppingList = new ShoppingList
            {
                Name = localizationService.GetTranslation("ShoppingList.NewShoppingList"),
                UserServer = userServer,
            };

            ShoppingLists.Add(shoppingList);
            shoppingListDbService.Add(shoppingList);

            return shoppingList;
        }

        public void RemoveUserShoppingList(ShoppingList shoppingList)
        {
            ShoppingLists.Remove(shoppingList);
            shoppingListDbService.Delete(shoppingList);
        }

        public void AddShoppingListRecipe(ShoppingList shoppingList, Recipe recipe, ShoppingListRecipe? parent = null, decimal quantityToCraft = 1)
        {
            var userShoppingListRecipe = new ShoppingListRecipe
            {
                Recipe = recipe,
                ShoppingList = shoppingList,
                ShoppingListCraftingTable = GetOrCreateShoppingListCraftingTable(shoppingList, recipe.CraftingTable),
                ShoppingListSkill = recipe.Skill is not null ? GetOrCreateShoppingListSkill(shoppingList, recipe.Skill) : null,
                QuantityToCraft = quantityToCraft,
                ParentShoppingListRecipe = parent,
            };

            shoppingList.ShoppingListRecipes.Add(userShoppingListRecipe);
            shoppingListRecipeDbService.Add(userShoppingListRecipe);
        }

        public void RemoveUserShoppingListRecipe(ShoppingList shoppingList, ShoppingListRecipe shoppingListRecipe)
        {
            shoppingList.ShoppingListRecipes.Remove(shoppingListRecipe);
            shoppingListRecipeDbService.Delete(shoppingListRecipe);
        }

        private ShoppingListCraftingTable GetOrCreateShoppingListCraftingTable(ShoppingList shoppingList, CraftingTable craftingTable)
        {
            var shoppingListCraftingTable = shoppingList.ShoppingListCraftingTables.Find(slct => slct.CraftingTable == craftingTable);

            if (shoppingListCraftingTable is not null)
            {
                return shoppingListCraftingTable;
            }

            return new ShoppingListCraftingTable
            {
                CraftingTable = craftingTable,
                PluginModule = craftingTable.CurrentUserCraftingTable?.PluginModule,
                ShoppingList = shoppingList,
            };
        }

        private ShoppingListSkill GetOrCreateShoppingListSkill(ShoppingList shoppingList, Skill skill)
        {
            var shoppingListSkill = shoppingList.ShoppingListSkills.Find(sls => sls.Skill == skill);

            if (shoppingListSkill is not null)
            {
                return shoppingListSkill;
            }

            return new ShoppingListSkill
            {
                Skill = skill,
                Level = skill.CurrentUserSkill?.Level ?? 0,
                HasLavishTalent = skill.CurrentUserSkill?.HasLavishTalent ?? false,
                ShoppingList = shoppingList,
            };
        }
    }
}
