using ecocraft.Models;

namespace ecocraft.Services;

public class ShoppingListService(EcoCraftDbContext dbContext)
{
    public async Task Calculate(List<ShoppingListRecipe> shoppingListRecipes, bool save = true)
    {
        if (save) await dbContext.SaveChangesAsync();

        foreach (var shoppingListRecipe in shoppingListRecipes)
        {
            // Ensures all element have a related ShoppingListItemOrTag
            foreach (var element in shoppingListRecipe.Recipe.Elements)
            {
                var relatedShoppingListItemOrTag = shoppingListRecipe.ShoppingListItemOrTags.Find(s => s.ItemOrTag == element.ItemOrTag);

                if (relatedShoppingListItemOrTag is null)
                {
                    relatedShoppingListItemOrTag = new ShoppingListItemOrTag
                    {
                        ItemOrTag = element.ItemOrTag,
                        IsIngredient = element.IsIngredient(),
                        ShoppingListRecipe = shoppingListRecipe,
                    };

                    shoppingListRecipe.ShoppingListItemOrTags.Add(relatedShoppingListItemOrTag);
                }

                if (element.IsProduct())
                {
                    relatedShoppingListItemOrTag.Quantity = element.Quantity.GetBaseValue() * shoppingListRecipe.QuantityToCraft;
                    relatedShoppingListItemOrTag.RemainingQuantity = 0;

                    var parentIngredient = shoppingListRecipe.ParentShoppingListRecipe?.ShoppingListItemOrTags.Find(s => s.ItemOrTag == element.ItemOrTag && s.IsIngredient);

                    if (parentIngredient is not null)
                    {
                        parentIngredient.RemainingQuantity -= relatedShoppingListItemOrTag.Quantity;
                    }
                }
                else
                {
                    relatedShoppingListItemOrTag.Quantity = Math.Round(-1 * element.Quantity.GetBaseValue() * shoppingListRecipe.QuantityToCraft * (shoppingListRecipe.ShoppingListCraftingTable?.PluginModule?.Percent ?? 1), MidpointRounding.ToPositiveInfinity) ;
                    relatedShoppingListItemOrTag.RemainingQuantity = relatedShoppingListItemOrTag.Quantity;
                }
            }

            await Calculate(shoppingListRecipe.ChildrenShoppingListRecipes, false);
        }

        if (save) await dbContext.SaveChangesAsync();
    }

    public List<ShoppingListItemOrTag> GetAllIngredients(List<ShoppingListRecipe> shoppingListRecipes)
    {
        var shoppingListItemOrTags = new List<ShoppingListItemOrTag>();

        foreach (var shoppingListRecipe in shoppingListRecipes)
        {
            var ingredients = shoppingListRecipe.ShoppingListItemOrTags.Where(sliot => sliot.IsIngredient && sliot.RemainingQuantity > 0);

            foreach (var ingredient in ingredients)
            {
                shoppingListItemOrTags.Add(ingredient);
            }

            var products = shoppingListRecipe.ShoppingListItemOrTags.Where(sliot => !sliot.IsIngredient && sliot.RemainingQuantity > 0);

            foreach (var product in products)
            {
                shoppingListItemOrTags.Add(new ShoppingListItemOrTag
                {
                    ItemOrTag = product.ItemOrTag,
                    IsIngredient = product.IsIngredient,
                    RemainingQuantity = -product.RemainingQuantity,
                });
            }

            if (shoppingListRecipe.ChildrenShoppingListRecipes.Count > 0)
            {
                shoppingListItemOrTags.AddRange(GetAllIngredients(shoppingListRecipe.ChildrenShoppingListRecipes));
            }
        }

        return shoppingListItemOrTags;
    }
}
