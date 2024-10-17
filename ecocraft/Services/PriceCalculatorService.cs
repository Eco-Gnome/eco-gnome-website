using ecocraft.Models;

namespace ecocraft.Services;

public class PriceCalculatorService(
    EcoCraftDbContext dbContext,
    ContextService contextService,
    UserServerDataService userServerDataService)
{
    public List<UserPrice> GetUserPricesToBuy(bool withoutItemsAssociatedToTags = false)
    {
        // Get all UserPrices where their ItemOrTag are not produced by any existing UserElement
        var list = userServerDataService.UserPrices
            .Where(up => !userServerDataService.UserElements
                .Where(ue =>
                    ue.Element.IsProduct() &&
                    ue.Element.Index == 0)
                //&& ue.Element.Recipe.Elements.Where(e => e.ItemOrTag == ue.Element.ItemOrTag).Select(x => x.Quantity).Sum() > 0)
                .Select(ue => ue.Element.ItemOrTag)
                .Contains(up.ItemOrTag)
            ).ToList();

        if (withoutItemsAssociatedToTags)
        {
            // Remove all UserPrices which ItemOrTag is associated to a Tag in the first list
            list = list.Where(up => !list.Where(i => i.ItemOrTag.IsTag)
                .SelectMany(i => i.ItemOrTag.AssociatedItems)
                .ToList()
                .Contains(up.ItemOrTag)
            ).ToList();
        }

        return list
            .OrderBy(o => contextService.GetTranslation(o.ItemOrTag))
            .ToList();
    }

    public Dictionary<UserPrice, List<UserElement>> GetUserPricesToSell()
    {
        // Get all UserPrices where their ItemOrTag is produced by any existing UserElement
        var userPricesToSell = userServerDataService.UserPrices
            .Where(up => userServerDataService.UserElements.Any(ue =>
                ue.Element.ItemOrTag == up.ItemOrTag && ue.Element.IsProduct() && ue.Element.Index == 0))
            .ToList();

        var output = new Dictionary<UserPrice, List<UserElement>>();

        foreach (var userPriceToSell in userPricesToSell)
        {
            output.Add(userPriceToSell,
                userServerDataService.UserElements.Where(ue =>
                    ue.Element.ItemOrTag == userPriceToSell.ItemOrTag && ue.Element.IsProduct()).ToList());
        }

        output = output.OrderBy(o => contextService.GetTranslation(o.Key.ItemOrTag))
            .ToDictionary();

        return output;
    }

    public async Task Calculate()
    {
        var userPriceAndElements = GetUserPricesToSell();
        var userPriceToBuy = GetUserPricesToBuy();

        foreach (var upe in userPriceAndElements)
        {
            upe.Key.Price = null;

            foreach (var element in upe.Value)
            {
                element.Price = null;
            }
        }

        foreach (var upb in userPriceToBuy)
        {
            if (upb.ItemOrTag.IsTag)
            {
                upb.Price = null;

                foreach (var item in upb.ItemOrTag.AssociatedItems)
                {
                    var itemPrice = userServerDataService.UserPrices.FirstOrDefault(up => up.ItemOrTag == item);

                    if (itemPrice is not null)
                    {
                        if (GetUserPricesToSell().Any(up => up.Key == itemPrice))
                        {
                            itemPrice.Price = null;
                        }
                        else
                        {
                            itemPrice.Price ??= 0;
                        }
                    }
                }
            }
            else
            {
                upb.Price ??= 0;
            }
        }

        foreach (var userElement in userServerDataService.UserElements)
        {
            userElement.Price = null;
        }

        // Calculate all UserPrices to Sell
        foreach (var upe in userPriceAndElements)
        {
            Console.WriteLine($"Calculate userPrice of Item {upe.Key.ItemOrTag.Name}");
            CalculateUserPrice(upe.Key, upe.Key, 1);
            Console.WriteLine($"Final calculation Item {upe.Key.ItemOrTag.Name} is {upe.Key.Price}");
        }

        await dbContext.SaveChangesAsync();
    }

    private void CalculateUserPrice(UserPrice userPrice, UserPrice masterUserPrice, int depth)
    {
        if (userPrice.Price is not null) return;

        Console.WriteLine($"{new string('\t', depth)}Calculate userPrice {userPrice.ItemOrTag.Name}");

        if (userPrice.ItemOrTag.IsTag)
        {
            if (userPrice.PrimaryUserPrice is not null)
            {
                if (userPrice.PrimaryUserPrice.Price is null)
                {
                    CalculateUserPrice(userPrice.PrimaryUserPrice, masterUserPrice, depth + 1);
                }

                userPrice.Price = userPrice.PrimaryUserPrice.Price;
                Console.WriteLine(
                    $"{new string('\t', depth)}Calculate userPrice {userPrice.ItemOrTag.Name} => Tag Primary: {userPrice.Price}");

                return;
            }

            List<UserPrice> userPrices = [];

            foreach (var item in userPrice.ItemOrTag.AssociatedItems)
            {
                var itemUserPrice = userServerDataService.UserPrices.FirstOrDefault(u => u.ItemOrTag == item);

                if (itemUserPrice.Price is null)
                {
                    CalculateUserPrice(itemUserPrice, masterUserPrice, depth + 1);
                }

                userPrices.Add(itemUserPrice);
            }

            // By default, we choose the minimum price.
            userPrice.Price = userPrices.Min(up => up.Price);

            Console.WriteLine(
                $"{new string('\t', depth)}Calculate userPrice {userPrice.ItemOrTag.Name} => Tag Min: {userPrice.Price}");

            return;
        }

        var relatedUserElements = userServerDataService.UserElements
            .Where(ue => ue.Element.IsProduct() && ue.Element.Index == 0 && ue.Element.ItemOrTag == userPrice.ItemOrTag)
            .ToList();

        foreach (var userElement in relatedUserElements)
        {
            CalculateUserElement(userElement, masterUserPrice, depth + 1);
        }

        if (userPrice.PrimaryUserElement is not null)
        {
            userPrice.Price = userPrice.PrimaryUserElement.Price;

            Console.WriteLine(
                $"{new string('\t', depth)}Calculate userPrice {userPrice.ItemOrTag.Name} => UserElements Primary {userPrice.Price}");
        }
        else
        {
            userPrice.Price = relatedUserElements.Max(r => r.Price);

            Console.WriteLine(
                $"{new string('\t', depth)}Calculate userPrice {userPrice.ItemOrTag.Name} => UserElements Max {userPrice.Price}");
        }
    }

    private void CalculateUserElement(UserElement userElement, UserPrice masterUserPrice, int depth)
    {
        if (userElement.Price is not null)
        {
            return;
        }

        Console.WriteLine(
            $"{new string('\t', depth)}Calculate userElement {userElement.Element.ItemOrTag.Name} of {userElement.Element.Recipe.Name}");

        var otherUserElements = userServerDataService.UserElements
            .Where(ue => ue.Element.Recipe == userElement.Element.Recipe)
            .ToList();
        var products = otherUserElements.Where(ue => ue.Element.IsProduct()).ToList();
        var ingredients = otherUserElements.Where(ue => ue.Element.IsIngredient()).ToList();

        foreach (var ingredient in ingredients)
        {
            if (ingredient.Price is not null) continue;

            var ingredientUserPrice =
                userServerDataService.UserPrices.First(up => up.ItemOrTag == ingredient.Element.ItemOrTag);

            if (ingredientUserPrice.Price is null)
            {
                if (ingredientUserPrice != masterUserPrice)
                {
                    CalculateUserPrice(ingredientUserPrice, masterUserPrice, depth + 1);
                    ingredient.Price = ingredientUserPrice.Price;
                    Console.WriteLine(
                        $"{new string('\t', depth)}Ingredient {ingredient.Element.ItemOrTag.Name} has calculated price {ingredient.Price}");
                }
                else
                {
                    Console.WriteLine(
                        $"{new string('\t', depth)}Circular recipes detected when trying to find {ingredientUserPrice.ItemOrTag.Name}");
                    return;
                }
            }
            else
            {
                ingredient.Price = ingredientUserPrice.Price;
                Console.WriteLine($"{new string('\t', depth)}Ingredient {ingredient.Element.ItemOrTag.Name} price from user price: {ingredientUserPrice.Price}");
            }
        }

        // Don't calculate any recipe with null price
        if (ingredients.Any(i => i.Price is null)) return;

        var pluginModulePercent = userServerDataService.UserCraftingTables
            .First(uct => uct.CraftingTable == userElement.Element.Recipe.CraftingTable).PluginModule?.Percent ?? 1;

        float lavishTalentValue = userElement.Element.Recipe.Skill?.LavishTalentValue ?? 1;

        var ingredientCostSum = -1 * ingredients.Sum(ing =>
            ing.Price * ing.Element.Quantity * (ing.Element.IsDynamic ? (userServerDataService.UserSkills.First(us => us.Skill == ing.Element.Skill).HasLavishTalent ? pluginModulePercent * lavishTalentValue : pluginModulePercent) : 1));

        // Remove ingredientCostSum from items that are bought
        foreach (var product in products.ToList())
        {
            var associatedUserPrice = userServerDataService.UserPrices
                .First(up => up.ItemOrTag == product.Element.ItemOrTag);

            if (!GetUserPricesToBuy().Contains(associatedUserPrice)) continue;

            Console.WriteLine(
                $"{new string('\t', depth)}Product {product.Element.ItemOrTag.Name} is a bought output, so we remove from ingredientCost: {associatedUserPrice.Price * product.Element.Quantity}");

            ingredientCostSum -= associatedUserPrice.Price * product.Element.Quantity *
                                 (product.Element.IsDynamic ? (userServerDataService.UserSkills.First(us => us.Skill == product.Element.Skill).HasLavishTalent ? pluginModulePercent * lavishTalentValue : pluginModulePercent) : 1);
            product.Price = (-1 * associatedUserPrice.Price * (product.Element.IsDynamic ? (userServerDataService.UserSkills.First(us => us.Skill == product.Element.Skill).HasLavishTalent ? pluginModulePercent * lavishTalentValue : pluginModulePercent) : 1));
            products.Remove(product);
        }

        var skillReducePercent = userElement.Element.Recipe.Skill?.LaborReducePercent[userServerDataService.UserSkills.First(us => us.Skill == userElement.Element.Recipe.Skill).Level];
        ingredientCostSum += userElement.Element.Recipe.Labor * userServerDataService.UserSetting!.CalorieCost / 1000 * skillReducePercent;

        Console.WriteLine(
            $"{new string('\t', depth)}Calculate userElement {userElement.Element.ItemOrTag.Name} of {userElement.Element.Recipe.Name} => ingredientCostSum {ingredientCostSum}");

        foreach (var product in products)
        {
            product.Price = ingredientCostSum * product.Share / product.Element.Quantity;

            Console.WriteLine(
                $"{new string('\t', depth)}Calculate userElement {product.Element.ItemOrTag.Name} of {product.Element.Recipe.Name} => {product.Price}");
        }
    }
}
