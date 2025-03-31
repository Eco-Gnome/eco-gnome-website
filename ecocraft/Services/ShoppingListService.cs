using ecocraft.Models;

namespace ecocraft.Services;

public class ShoppingListService
{
    public Dictionary<ItemOrTag, decimal> GetAggregatedOutputs(DataContext shoppingList, List<UserRecipe> shoppingListRecipes, Dictionary<ItemOrTag, decimal>? outputs = null, int depth = 0)
    {
        outputs ??= new Dictionary<ItemOrTag, decimal>();

        foreach (var shoppingListRecipe in shoppingListRecipes)
        {
            foreach (var userElement in shoppingListRecipe.Recipe.Elements.OrderBy(e => (e.IsProduct() ? 0 : 1000) + e.Index))
            {
                var currentValue = 0m;
                var currentItemOrTag = userElement.ItemOrTag;

                foreach (var itemOrTag in userElement.ItemOrTag.GetAssociatedTagsAndSelf())
                {
                    var someValue = outputs.GetValueOrDefault(itemOrTag, 0);

                    if (someValue != 0)
                    {
                        currentValue = someValue;
                        currentItemOrTag = itemOrTag;
                        break;
                    }
                }

                outputs[currentItemOrTag] = currentValue + userElement.Quantity.GetDynamicValue(shoppingList) * shoppingListRecipe.RoundFactor;
            }

            if (shoppingListRecipe.ChildrenUserRecipes.Count > 0)
            {
                GetAggregatedOutputs(shoppingList, shoppingListRecipe.ChildrenUserRecipes, outputs, depth + 4);
            }
        }

        return outputs;
    }
}
