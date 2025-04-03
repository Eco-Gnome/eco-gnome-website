using ecocraft.Models;

namespace ecocraft.Services.DbServices;

public class ShoppingListRecipeDbService(EcoCraftDbContext context)
{
    public ShoppingListRecipe Add(ShoppingListRecipe shoppingListRecipe)
    {
        context.ShoppingListRecipes.Add(shoppingListRecipe);
        return shoppingListRecipe;
    }

    public void Update(ShoppingListRecipe shoppingListRecipe)
    {
        context.ShoppingListRecipes.Update(shoppingListRecipe);
    }

    public void Delete(ShoppingListRecipe shoppingListRecipe)
    {
        context.ShoppingListRecipes.Remove(shoppingListRecipe);
    }
}
