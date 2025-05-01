using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services;

public class ShoppingListService(
    EcoCraftDbContext dbContext,
    UserElementDbService userElementDbService)
{
    public async Task Calculate(List<UserRecipe> shoppingListRecipes, bool save = true)
    {
        if (save) await dbContext.SaveChangesAsync();

        foreach (var shoppingListRecipe in shoppingListRecipes)
        {
            foreach (var element in shoppingListRecipe.Recipe.Elements)
            {
                var userElement = shoppingListRecipe.DataContext.UserElements.Find(s => s.Element == element);

                // Ensures all element have a related UserElement
                if (userElement is null)
                {
                    userElement = new UserElement
                    {
                        Element = element,
                    };

                    shoppingListRecipe.DataContext.UserElements.Add(userElement);
                    userElementDbService.Add(userElement);
                }

                if (element.IsProduct())
                {
                    userElement.SLQuantity = element.Quantity.GetBaseValue() * shoppingListRecipe.RoundFactor;
                    userElement.SLRemainingQuantity = 0;

                    var parentIngredient = shoppingListRecipe.ParentUserRecipe?.Recipe.Elements.First(s => s.ItemOrTag == element.ItemOrTag && s.IsIngredient()).CurrentUserElement;

                    if (parentIngredient is not null)
                    {
                        parentIngredient.SLRemainingQuantity -= userElement.SLQuantity;
                    }
                }
                else
                {
                    userElement.SLQuantity = Math.Round(-1
                                                        * element.Quantity.GetBaseValue()
                                                        * shoppingListRecipe.RoundFactor
                                                        * (shoppingListRecipe.Recipe.CraftingTable.CurrentUserCraftingTable?.PluginModule?.GetPercent(shoppingListRecipe.Recipe.Skill) ?? 1),
                        MidpointRounding.ToPositiveInfinity) ;
                    userElement.SLRemainingQuantity = userElement.SLQuantity;
                }
            }

            await Calculate(shoppingListRecipe.ChildrenUserRecipes, false);
        }

        if (save) await dbContext.SaveChangesAsync();
    }

    public List<UserElement> GetAllIngredients(List<UserRecipe> shoppingListRecipes)
    {
        var userElements = new List<UserElement>();

        foreach (var shoppingListRecipe in shoppingListRecipes)
        {
            var ingredients = shoppingListRecipe.Recipe.Elements.Where(e => e.IsIngredient() && e.CurrentUserElement!.SLRemainingQuantity > 0).Select(e => e.CurrentUserElement!);

            foreach (var ingredient in ingredients)
            {
                userElements.Add(ingredient);
            }

            var products = shoppingListRecipe.Recipe.Elements.Where(e => !e.IsIngredient() && e.CurrentUserElement!.SLRemainingQuantity > 0);

            foreach (var product in products)
            {
                userElements.Add(new UserElement
                {
                    Element = product,
                    SLRemainingQuantity = -product.CurrentUserElement!.SLRemainingQuantity,
                });
            }

            if (shoppingListRecipe.ChildrenUserRecipes.Count > 0)
            {
                userElements.AddRange(GetAllIngredients(shoppingListRecipe.ChildrenUserRecipes));
            }
        }

        return userElements;
    }
}
