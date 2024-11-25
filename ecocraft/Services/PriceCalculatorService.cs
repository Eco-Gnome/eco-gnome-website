using ecocraft.Models;

namespace ecocraft.Services;

public class PriceCalculatorService(
    EcoCraftDbContext dbContext,
    UserServerDataService userServerDataService,
    LocalizationService localizationService)
{
    public (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTags()
    {
        // Get all UserPrices where their ItemOrTag are not produced by any existing UserElement
        var listOfProducts = userServerDataService.UserElements
            .Where(ue => ue.Element.IsProduct() && !ue.IsReintegrated && !userServerDataService.UserPrices.First(up => up.ItemOrTag == ue.Element.ItemOrTag).OverrideIsBought)
            .Select(ue => ue.Element.ItemOrTag)
            .Distinct()
            .ToList();

        var listOfIngredients = userServerDataService.UserElements
            .Where(ue => !listOfProducts.Contains(ue.Element.ItemOrTag))
            .Select(ue => ue.Element.ItemOrTag)
            .Concat(
                userServerDataService.UserPrices
                    .Where(up => up.OverrideIsBought)
                    .Select(up => up.ItemOrTag)
            )
            .Distinct()
            .ToList();

        return (listOfIngredients, listOfProducts);
    }

    public (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTagsForDisplay()
    {
        var (listOfIngredients, listOfProducts) = GetCategorizedItemOrTags();

        listOfIngredients = listOfIngredients.Where(i => !i.AssociatedTags.Intersect(listOfIngredients).Any())
            .OrderBy(n => localizationService.GetTranslation(n))
            .ToList();

        listOfProducts = listOfProducts
            .OrderBy(n => localizationService.GetTranslation(n))
            .ToList();

        return (listOfIngredients, listOfProducts);
    }

    public async Task Calculate(bool debug = false)
    {
        // Reset Prices
        var (_, itemOrTagsToSell) = GetCategorizedItemOrTags();

        userServerDataService.UserElements.ForEach(ue => ue.Price = null);
        userServerDataService.UserPrices
            .Where(up => (itemOrTagsToSell.Contains(up.ItemOrTag) || up.ItemOrTag.IsTag) && !up.OverrideIsBought)
            .ToList()
            .ForEach(up => up.Price = null);

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

                var userElementIngredients = userRecipe.Recipe.Elements.Where(e => e.IsIngredient()).Select(e => e.CurrentUserElement!).ToList();

                // Try to find ingredient price, based on it's associated UserPrice, or if it's a tag, based on it's related Items
                foreach (var ingredient in userElementIngredients.Where(ue => ue.Price is null).ToList())
                {
                    if (ingredient.Element.ItemOrTag.CurrentUserPrice!.Price is not null)
                    {
                        ingredient.Price = ingredient.Element.ItemOrTag.CurrentUserPrice.Price;
                        if (debug) Console.WriteLine($"=> Ingredient {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                        continue;
                    }

                    if (!ingredient.Element.ItemOrTag.CurrentUserPrice.ItemOrTag.IsTag) continue;

                    if (ingredient.Element.ItemOrTag.CurrentUserPrice.PrimaryUserPrice?.Price is not null)
                    {
                        ingredient.Element.ItemOrTag.CurrentUserPrice.Price = ingredient.Element.ItemOrTag.CurrentUserPrice.PrimaryUserPrice.Price;
                        ingredient.Price = ingredient.Element.ItemOrTag.CurrentUserPrice.Price;
                        if (debug) Console.WriteLine($"=> Ingredient Tag (from primary) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                        continue;
                    }

                    var associatedItemsUserPrices = ingredient.Element.ItemOrTag.CurrentUserPrice.ItemOrTag.AssociatedItems.Select(iot => iot.CurrentUserPrice!).ToList();

                    if (!associatedItemsUserPrices.All(up => up.Price is not null)) continue;

                    ingredient.Element.ItemOrTag.CurrentUserPrice.Price = associatedItemsUserPrices.Select(up => up.Price).Min();
                    ingredient.Price = ingredient.Element.ItemOrTag.CurrentUserPrice.Price;
                    if (debug) Console.WriteLine($"=> Ingredient Tag (from Min) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");
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

                var userElementProducts = userRecipe.Recipe.Elements.Where(e => e.IsProduct()).Select(e => e.CurrentUserElement!).ToList();
                var reintegratedProducts = userElementProducts.Where(ue => ue.IsReintegrated).ToList();

                if (reintegratedProducts.Any(ue => ue.Element.ItemOrTag.CurrentUserPrice!.Price is null))
                {
                    iterator++;

                    if (debug)
                    {
                        reintegratedProducts.Where(ue => ue.Element.ItemOrTag.CurrentUserPrice!.Price is null).ToList().ForEach(ue => Console.WriteLine($"=> Reintegrated Product {ue.Element.ItemOrTag.Name} is null"));
                        Console.WriteLine("=> Stop");
                    }

                    continue;
                }

                if (debug) Console.WriteLine("=> Calc");

                remainingUserRecipes.RemoveAt(iterator);

                var pluginModulePercent = userRecipe.Recipe.CraftingTable.CurrentUserCraftingTable!.PluginModule?.Percent ?? 1;
                var lavishTalentValue = userRecipe.Recipe.Skill?.CurrentUserSkill!.HasLavishTalent ?? false
                    ? userRecipe.Recipe.Skill.LavishTalentValue ?? 1
                    : 1;

                var dynamicReduction = pluginModulePercent * lavishTalentValue;

                var ingredientCostSum = -1 * userElementIngredients.Sum(ue => ue.Price * ue.GetRoundFactorQuantity(ue.Element.IsDynamic ? dynamicReduction : 1));

                // We remove from ingredientCostSum, the price of reintegrated products
                foreach (var reintegratedProduct in reintegratedProducts)
                {
                    reintegratedProduct.Price = -1 * reintegratedProduct.Element.ItemOrTag.CurrentUserPrice!.Price;
                    ingredientCostSum += reintegratedProduct.Price * reintegratedProduct.GetRoundFactorQuantity(reintegratedProduct.Element.IsDynamic ? dynamicReduction : 1);
                }

                var skillReducePercent = userRecipe.Recipe.Skill?.LaborReducePercent[userRecipe.Recipe.Skill.CurrentUserSkill!.Level] ?? 1;
                ingredientCostSum += userRecipe.Recipe.Labor * userServerDataService.UserSetting!.CalorieCost / 1000 * skillReducePercent;

                var craftMinuteFee = userRecipe.Recipe.CraftingTable.CurrentUserCraftingTable.CraftMinuteFee;

                ingredientCostSum += craftMinuteFee * userRecipe.Recipe.CraftMinutes * pluginModulePercent;
                // TODO: add talent related to craft time

                foreach (var product in userElementProducts.Where(p => p.Price is null).ToList())
                {
                    // Calculate the associated User price if needed
                    var finalQuantity = product.GetRoundFactorQuantity(product.Element.IsDynamic ? dynamicReduction! : 1m);
                    product.Price = ingredientCostSum * product.Share / finalQuantity;

                    if (debug) Console.WriteLine($"=> Product {product.Element.ItemOrTag.Name}: {product.Price}");

                    if (product.Element.ItemOrTag.CurrentUserPrice!.OverrideIsBought) continue;
                    if (product.Element.ItemOrTag.CurrentUserPrice!.Price is not null) continue;

                    // We choose the PrimaryUserElement.Price
                    if (product.Element.ItemOrTag.CurrentUserPrice!.PrimaryUserElement == product)
                    {
                        product.Element.ItemOrTag.CurrentUserPrice!.Price = product.Price;
                    }
                    else if (product.Element.ItemOrTag.CurrentUserPrice!.PrimaryUserElement is null)
                    {
                        // Or we choose the min price of all related user element
                        var relatedUserElements = userServerDataService.UserElements.Where(ue => ue.Element.ItemOrTag == product.Element.ItemOrTag.CurrentUserPrice!.ItemOrTag
                                                                                                 && ue.Element.IsProduct()
                                                                                                 && !ue.IsReintegrated).ToList();

                        if (relatedUserElements.All(ue => ue.Price is not null))
                        {
                            product.Element.ItemOrTag.CurrentUserPrice!.Price = relatedUserElements.Min(ue => ue.Price);
                        }
                    }
                }

                nbHandled++;
            }
        } while (nbHandled > 0);

        await dbContext.SaveChangesAsync();
    }
}
