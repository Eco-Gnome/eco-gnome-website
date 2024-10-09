using ecocraft.Models;

namespace ecocraft.Services;

public class PriceCalculatorService(
    ContextService contextService,
    ServerDataService serverDataService,
    UserServerDataService userServerDataService)
{
    public List<UserPrice> GetUserPricesToBuy()
    {
        // Get all UserPrices where their ItemOrTag are not produced by any existing UserElement
        return userServerDataService.UserPrices
            .Where(up => !userServerDataService.UserElements
                .Where(ue => ue.Element.IsProduct())
                .Select(ue => ue.Element.ItemOrTag)
                .Contains(up.ItemOrTag)
            )
            .OrderBy(o => contextService.GetTranslation(o.ItemOrTag))
            .ToList();
    }

    public Dictionary<UserPrice, List<UserElement>> GetUserPricesToSell()
    {
        // Get all UserPrices where their ItemOrTag is produced by any existing UserElement
        var userPricesToSell = userServerDataService.UserElements
            .Where(ue => ue.Element.IsProduct())
            .Select(ue => ue.Element.ItemOrTag.UserPrices.First())
            .Distinct()
            .ToList();

        var output = new Dictionary<UserPrice, List<UserElement>>();
        
        foreach (var userPriceToSell in userPricesToSell)
        {
            output.Add(userPriceToSell, userServerDataService.UserElements.Where(ue => ue.Element.ItemOrTag == userPriceToSell.ItemOrTag && ue.Element.IsProduct()).ToList());
        }

        output = output.OrderBy(o => contextService.GetTranslation(o.Key.ItemOrTag))
            .ToDictionary();

        return output;
    }

    public void Calculate()
    {
        var userPriceAndElements = GetUserPricesToSell();

        foreach (var upe in userPriceAndElements)
        {
            upe.Key.Price = null;
            
            foreach (var element in upe.Value)
            {
                element.Price = null;
            };
        }
        
        // Calculate all UserPrices to Sell
        foreach (var upe in userPriceAndElements)
        {
            CalculateUserPrice(upe.Key, upe.Value);
        }
    }

    private void CalculateUserPrice(UserPrice userPrice, List<UserElement> relatedUserElements)
    {
        
    }

    /*public float CalculateItemOrTagPrice(ItemOrTag itemOrTag)
    {
        var price = itemOrTag.UserPrices.FirstOrDefault()?.Price;

        if (price is not null)
        {
            return price ?? 0;
        }

        if (itemOrTag.IsTag)
        {
            var itemPrices = itemOrTag.AssociatedItemOrTags.Select(i => i.UserPrices.FirstOrDefault()?.Price)
                .Where(i => i != null).ToList();

            if (itemPrices.Count > 0)
            {
                // By default, for a tag price, we choose the maximum price of associated items
                return itemPrices.Max() ?? 0;
            }
        }

        return 0;
    }*/
}