using ecocraft.Models;

namespace ecocraft.Services;

public class PriceCalculatorService(
    ServerDataService serverDataService,
    UserDataService userDataService)
{
    public List<ItemOrTag> ListItemOrTagsToBuy()
    {
        var selectedElements = userDataService.UserRecipes
            .Select(ur => ur.Recipe)
            .SelectMany(r => r.Elements)
            .ToList();
        
        var listOfProducts = selectedElements
            .Where(e => e.IsProduct())
            .Select(e => e.ItemOrTag)
            .ToList();
        
        return selectedElements
            .Where(e => e.IsIngredient())
            .Select(e => e.ItemOrTag)
            .Where(i => !listOfProducts.Contains(i))
            .ToList();
    }
    
    public List<ItemOrTag> ListItemOrTagsToSell()
    {
        var selectedElements = userDataService.UserRecipes
            .Select(ur => ur.Recipe)
            .SelectMany(r => r.Elements)
            .ToList();
        
        return selectedElements
            .Where(e => e.IsProduct())
            .Select(e => e.ItemOrTag)
            .ToList();
    }
    
    public float CalculateItemOrTagPrice(ItemOrTag itemOrTag)
    {
        var price = itemOrTag.UserPrices.FirstOrDefault()?.Price;
        
        if (price is not null)
        {
            return price ?? 0;
        }

        if (itemOrTag.IsTag)
        {
            var itemPrices = itemOrTag.AssociatedItemOrTags.Select(i => i.UserPrices.FirstOrDefault()?.Price).Where(i => i != null).ToList();

            if (itemPrices.Count > 0)
            {
                // By default, for a tag price, we choose the maximum price of associated items
                return itemPrices.Max() ?? 0;
            }
        }

        return 0;
    }
}