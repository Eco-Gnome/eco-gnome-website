using ecocraft.Models;

namespace ecocraft.Services;

public class PriceCalculatorService(
    EcoCraftDbContext dbContext,
    UserServerDataService userServerDataService,
    LocalizationService localizationService)
{
    public (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTags(DataContext dataContext)
    {
        // Get all UserPrices where their ItemOrTag are not produced by any existing UserElement
        var listOfProducts = dataContext.UserElements
            .Where(ue => ue.Element.IsProduct() && !ue.IsReintegrated && !(ue.Element.ItemOrTag.GetCurrentUserPrice(dataContext)?.OverrideIsBought ?? false))
            .Select(ue => ue.Element.ItemOrTag)
            .Distinct()
            .ToList();

        var listOfIngredients = dataContext.UserElements
            .Where(ue => !listOfProducts.Contains(ue.Element.ItemOrTag))
            .Select(ue => ue.Element.ItemOrTag)
            .Concat(
                dataContext.UserPrices
                    .Where(up => up.OverrideIsBought)
                    .Select(up => up.ItemOrTag)
            )
            .Distinct()
            .ToList();

        return (listOfIngredients, listOfProducts);
    }

    public (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTagsForDisplay(DataContext dataContext)
    {
        var (listOfIngredients, listOfProducts) = GetCategorizedItemOrTags(dataContext);

        listOfIngredients = listOfIngredients.Where(i => !i.AssociatedTags.Intersect(listOfIngredients).Any())
            .OrderBy(localizationService.GetTranslation)
            .ToList();

        listOfProducts = listOfProducts
            .OrderBy(i =>
            {
                var relatedElements = i.Elements.Where(element => element.GetCurrentUserElement(dataContext) is not null && element.IsProduct() && !element.GetCurrentUserElement(dataContext)!.IsReintegrated).ToList();
                var bestRelatedElement = relatedElements.Find(element => element.Index == 0) ?? relatedElements.FirstOrDefault();
                return localizationService.GetTranslation(bestRelatedElement?.Recipe.Skill);
            })
            .ThenBy(localizationService.GetTranslation)
            .ToList();

        return (listOfIngredients, listOfProducts);
    }

    public async Task Calculate(DataContext dataContext, bool debug = false)
    {
        // Reset Prices
        var (_, itemOrTagsToSell) = GetCategorizedItemOrTags(dataContext);

        userServerDataService.UserRecipes.ForEach(ur => ur.UserElements.ForEach(ue => ue.Price = null));
        userServerDataService.UserPrices
            .Where(up => (itemOrTagsToSell.Contains(up.ItemOrTag) || up.ItemOrTag.IsTag) && !up.OverrideIsBought)
            .ToList()
            .ForEach(up => up.Price = null);

        userServerDataService.UserPrices.ForEach(up => up.MarginPrice = null);

        var marginType = userServerDataService.UserSetting!.MarginType;
        List<UserRecipe> remainingUserRecipes = userServerDataService.UserRecipes.ToList();
        int nbHandled;

        do
        {
            nbHandled = 0;
            int iterator = 0;

            while (remainingUserRecipes.Count > 0 && iterator < remainingUserRecipes.Count)
            {
                var userRecipe = remainingUserRecipes[iterator];
                if (debug) Console.WriteLine($"Check Recipe: {userRecipe.Recipe.Name}");

                var userElementIngredients = userRecipe.Recipe.Elements.Where(e => e.IsIngredient()).Select(e => e.GetCurrentUserElement(dataContext)!).ToList();

                // Try to find ingredient price, based on it's associated UserPrice, or if it's a tag, based on it's related Items
                foreach (var ingredient in userElementIngredients.Where(ue => ue.Price is null).ToList())
                {
                    if (ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price is not null)
                    {
                        SetPriceOrMarginPrice(dataContext, ingredient, ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);
                        if (debug) Console.WriteLine($"=> Ingredient {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                        continue;
                    }

                    if (!ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.ItemOrTag.IsTag) continue;

                    if (ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserPrice?.Price is not null)
                    {
                        ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price = ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserPrice!.Price;
                        ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.MarginPrice = ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserPrice!.MarginPrice;

                        SetPriceOrMarginPrice(dataContext, ingredient, ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);
                        if (debug) Console.WriteLine($"=> Ingredient Tag (from primary) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                        continue;
                    }

                    var associatedItemsUserPrices = ingredient.Element.ItemOrTag.AssociatedItems
                        .Select(iot => iot.GetCurrentUserPrice(dataContext)!)
                        .ToList();

                    if (!associatedItemsUserPrices.All(up => up.Price is not null)) continue;

                    ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price = associatedItemsUserPrices.Min(up => up.Price);
                    ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.MarginPrice = associatedItemsUserPrices.First(up => up.Price == ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price).MarginPrice;
                    SetPriceOrMarginPrice(dataContext, ingredient, ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);

                    if (debug) Console.WriteLine($"=> Ingredient Tag (from Min) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");
                }

                var userElementProducts = userRecipe.Recipe.Elements.Where(e => e.IsProduct()).Select(e => e.GetCurrentUserElement(dataContext)!).ToList();
                var reintegratedProducts = userElementProducts.Where(ue => ue.IsReintegrated).ToList();

                // We calculate the element price of reintegrated products
                foreach (var reintegratedProduct in reintegratedProducts)
                {
                    SetPriceOrMarginPrice(dataContext, reintegratedProduct, reintegratedProduct.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);
                    reintegratedProduct.Price *= -1;
                }

                if (userElementIngredients.Any(ue => ue.Price is null))
                {
                    iterator++;

                    if (debug)
                    {
                        userElementIngredients.Where(ue => ue.Price is null).ToList().ForEach(ue => Console.WriteLine($"=> Ingredient {ue.Element.ItemOrTag.Name} is null"));
                        Console.WriteLine("=> Stop");
                    }

                    continue;
                }

                if (reintegratedProducts.Any(ue => ue.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price is null))
                {
                    iterator++;

                    if (debug)
                    {
                        reintegratedProducts.Where(ue => ue.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price is null).ToList().ForEach(ue => Console.WriteLine($"=> Reintegrated Product {ue.Element.ItemOrTag.Name} is null"));
                        Console.WriteLine("=> Stop");
                    }

                    continue;
                }

                if (debug) Console.WriteLine("=> Calc");

                remainingUserRecipes.RemoveAt(iterator);

                var ingredientCostSum = -1 * userElementIngredients.Sum(ue => ue.Price * ue.Element.Quantity.GetRoundFactorDynamicValue(dataContext));
                // We remove from ingredientCostSum, the price of reintegrated products
                ingredientCostSum += reintegratedProducts.Sum(ue => ue.Price * ue.Element.Quantity.GetRoundFactorDynamicValue(dataContext));
                // We add the labor cost
                ingredientCostSum += userRecipe.Recipe.Labor.GetDynamicValue(dataContext) * userServerDataService.UserSetting!.CalorieCost / 1000;
                // We add the craft minute fee
                ingredientCostSum += userRecipe.Recipe.CraftingTable.GetCurrentUserCraftingTable(dataContext)!.CraftMinuteFee * userRecipe.Recipe.CraftMinutes.GetDynamicValue(dataContext);

                foreach (var product in userElementProducts.Where(p => p.Price is null).ToList())
                {
                    // Calculate the associated User price if needed
                    var finalQuantity = product.Element.Quantity.GetRoundFactorDynamicValue(dataContext);
                    product.Price = ingredientCostSum * product.Share / finalQuantity;

                    if (debug) Console.WriteLine($"=> Product {product.Element.ItemOrTag.Name}: {product.Price}");

                    if (product.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.OverrideIsBought) continue;
                    if (product.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price is not null) continue;

                    // We choose the PrimaryUserElement.Price
                    if (product.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserElement == product)
                    {
                        product.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.SetPrices(product.Price, marginType);
                    }
                    else if (product.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserElement is null)
                    {
                        // Or we choose the min price of all related user element
                        var relatedUserElements = product.Element.ItemOrTag.Elements.Where(e => e.IsProduct()).Select(e => e.GetCurrentUserElement(dataContext)).Where(ue => ue is not null && !ue.IsReintegrated).ToList();

                        if (relatedUserElements.All(ue => ue!.Price is not null))
                        {
                            product.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.SetPrices(relatedUserElements.Min(ue => ue!.Price), marginType);
                        }
                    }
                }

                nbHandled++;
            }
        } while (nbHandled > 0);

        await dbContext.SaveChangesAsync();
    }

    private void SetPriceOrMarginPrice(DataContext dataContext, UserElement ingredient, UserPrice userPrice, UserRecipe userRecipe)
    {
        var recipesThatProduceMyIngredient = ingredient.Element.ItemOrTag.GetAssociatedItemsAndSelf().SelectMany(i => i.Elements)
            .Where(e => e.IsProduct())
            .Select(e => e.GetCurrentUserElement(dataContext))
            .Where(ue => ue is not null)
            .Select(ue => ue!.UserRecipe.Recipe)
            .Where(r => r.Skill is not null)
            .ToList();

        /*var recipesThatProduceMyIngredient2 = userServerDataService.UserElements
            .Where(ue => ue.Element.IsProduct() && (ingredient.Element.ItemOrTag.IsTag
                ? ingredient.Element.ItemOrTag.AssociatedItems.Contains(ue.Element.ItemOrTag)
                : ue.Element.ItemOrTag == ingredient.Element.ItemOrTag))
            .Select(ue => ue.Element.Recipe)
            .Where(r => r.Skill is not null && r.GetCurrentUserRecipe(dataContext) is not null)
            .ToList();*/

        if (userServerDataService.UserSetting!.ApplyMarginBetweenSkills
            && userPrice is { OverrideIsBought: false, MarginPrice: not null }
            && recipesThatProduceMyIngredient.Count > 0
            && recipesThatProduceMyIngredient.Any(r => r.Skill != userRecipe.Recipe.Skill))
        {
            ingredient.Price = userPrice.MarginPrice;
            ingredient.IsMarginPrice = true;
        }
        else
        {
            ingredient.Price = userPrice.Price;
            ingredient.IsMarginPrice = false;
        }
    }
}
