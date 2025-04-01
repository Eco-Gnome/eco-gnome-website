using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services
{
    public class UserShoppingListsDataService(
    UserShoppingListDbService userShoppingListDbService,
    UserShoppingListRecipeDbService userShoppingListRecipeDbService,
    UserShoppingListElementDbService userShoppingListElementDbService,
    UserServerDataService userServerDataService,
    LocalizationService localizationService)
    {
        public List<UserShoppingList> UserShoppingLists { get; private set; } = [];

        public async Task RetrieveUserData(UserServer? userServer)
        {
            if (userServer is null)
            {
                UserShoppingLists = [];

                return;
            }

            var userShoppingListsTask = userShoppingListDbService.GetByUserServerAsync(userServer);

            await Task.WhenAll(userShoppingListsTask);

            UserShoppingLists = userShoppingListsTask.Result;            
        }

        public void createUserShoppingList(UserServer userServer)
        {
            var userShoppingList = new UserShoppingList
            {
                Name = localizationService.GetTranslation("UserServerDataService.NewShoppingList"),
                UserServer = userServer,
            };

            UserShoppingLists.Add(userShoppingList);
            userShoppingListDbService.Add(userShoppingList);
        }

        public void removeUserShoppingList(UserShoppingList userShoppingList)
        {
            UserShoppingLists.Remove(userShoppingList);
            userShoppingListDbService.Delete(userShoppingList);
        }

        public void addUserShoppingListRecipe(UserShoppingList userShoppingList, Recipe recipe)
        {
            var userShoppingListRecipe = new UserShoppingListRecipe
            {
                Recipe = recipe,
                UserShoppingList = userShoppingList,
                UserCraftingTable = recipe.CraftingTable.CurrentUserCraftingTable,
                QuantityToCraft = 1
            };

            userShoppingList.UserShoppingListRecipes.Add(userShoppingListRecipe);
            userShoppingListRecipeDbService.Update(userShoppingListRecipe);
        }

        public void removeUserShoppingListRecipe(UserShoppingList userShoppingList, UserShoppingListRecipe userShoppingListRecipe)
        {

            userShoppingList.UserShoppingListRecipes.Remove(userShoppingListRecipe);
            userShoppingListRecipeDbService.Delete(userShoppingListRecipe);
        }   

    }
}
