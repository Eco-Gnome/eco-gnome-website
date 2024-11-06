using ecocraft.Models;

namespace ecocraft.Services;

public class PriceCalculatorService(
    EcoCraftDbContext dbContext,
    ContextService contextService,
    UserServerDataService userServerDataService)
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
            .OrderBy(n => contextService.GetTranslation(n))
            .ToList();

        listOfProducts = listOfProducts
            .OrderBy(n => contextService.GetTranslation(n))
            .ToList();

        return (listOfIngredients, listOfProducts);
    }

    public async Task Calculate(bool debug = false)
    {
        // Reset Prices
        var (_, itemOrTagsToSell) = GetCategorizedItemOrTags();

        userServerDataService.UserElements
            .Where(ue => !userServerDataService.UserPrices.First(up => up.ItemOrTag == ue.Element.ItemOrTag).OverrideIsBought)
            .ToList()
            .ForEach(ue => ue.Price = null);

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

                var userElementIngredients = userServerDataService.UserElements.Where(ue => ue.Element.IsIngredient() && ue.Element.Recipe == userRecipe.Recipe).ToList();

                // Try to find ingredient price, based on it's associated UserPrice, or if it's a tag, based on it's related Items
                foreach (var ingredient in userElementIngredients.Where(ue => ue.Price is null).ToList())
                {
                    var associatedUserPrice = userServerDataService.UserPrices.First(up => up.ItemOrTag == ingredient.Element.ItemOrTag);

                    if (associatedUserPrice.Price is not null)
                    {
                        ingredient.Price = associatedUserPrice.Price;
                        if (debug) Console.WriteLine($"=> Ingredient {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                        continue;
                    }

                    if (!associatedUserPrice.ItemOrTag.IsTag) continue;

                    if (associatedUserPrice.PrimaryUserPrice?.Price is not null)
                    {
                        associatedUserPrice.Price = associatedUserPrice.PrimaryUserPrice.Price;
                        ingredient.Price = associatedUserPrice.Price;
                        if (debug) Console.WriteLine($"=> Ingredient Tag (from primary) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                        continue;
                    }

                    var associatedItemsUserPrices = associatedUserPrice.ItemOrTag.AssociatedItems.Select(iot => userServerDataService.UserPrices.First(up => up
                        .ItemOrTag == iot)).ToList();

                    if (!associatedItemsUserPrices.All(up => up.Price is not null)) continue;

                    associatedUserPrice.Price = associatedItemsUserPrices.Select(up => up.Price).Min();
                    ingredient.Price = associatedUserPrice.Price;
                    if (debug) Console.WriteLine($"=> Ingredient Tag (from Min) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");
                }

                if (userElementIngredients.Any(ue => ue.Price is null))
                {
                    iterator++;
                    if (debug) userElementIngredients.Where(ue => ue.Price is null).ToList().ForEach(ue => Console.WriteLine($"=> Ingredient {ue.Element.ItemOrTag.Name} is null"));
                    if (debug) Console.WriteLine($"=> Stop");
                    continue;
                }

                var userElementProducts = userServerDataService.UserElements.Where(ue => ue.Element.IsProduct() && ue.Element.Recipe == userRecipe.Recipe).ToList();
                var reintegratedProducts = userElementProducts.Where(ue => ue.IsReintegrated).ToList();

                if (reintegratedProducts.Any(ue => userServerDataService.UserPrices.First(up => up.ItemOrTag == ue.Element.ItemOrTag).Price is null))
                {
                    iterator++;
                    reintegratedProducts.Where(ue => userServerDataService.UserPrices.First(up => up.ItemOrTag == ue.Element.ItemOrTag).Price is null).ToList().ForEach(ue => Console.WriteLine($"=> Reintegrated Product {ue.Element.ItemOrTag.Name} is null"));
                    if (debug) Console.WriteLine($"=> Stop");
                    continue;
                }

                if (debug) Console.WriteLine($"=> Calc");

                remainingUserRecipes.RemoveAt(iterator);

                var pluginModulePercent = userServerDataService.UserCraftingTables.First(uct => uct.CraftingTable == userRecipe.Recipe.CraftingTable).PluginModule?.Percent ?? 1;
                var lavishTalentValue = userRecipe.Recipe.Skill?.LavishTalentValue ?? 1f;

                var ingredientCostSum = -1 * userElementIngredients.Sum(ue => ue.Price * ue.Element.Quantity * (ue.Element.IsDynamic
                    ? userServerDataService.UserSkills.First(us => us.Skill == ue.Element.Skill).HasLavishTalent
                        ? pluginModulePercent * lavishTalentValue
                        : pluginModulePercent
                    : 1));

                // We remove from ingredientCostSum, the price of reintegrated products
                foreach (var reintegratedProduct in reintegratedProducts)
                {
                    var associatedUserPrice = userServerDataService.UserPrices.First(up => up.ItemOrTag == reintegratedProduct.Element.ItemOrTag);

                    reintegratedProduct.Price = -1 * associatedUserPrice.Price * (reintegratedProduct.Element.IsDynamic
                        ? userServerDataService.UserSkills.First(us => us.Skill == reintegratedProduct.Element.Skill).HasLavishTalent
                            ? pluginModulePercent * lavishTalentValue
                            : pluginModulePercent
                        : 1);
                    ingredientCostSum += reintegratedProduct.Price * reintegratedProduct.Element.Quantity;
                }

                var skillReducePercent = userRecipe.Recipe.Skill?.LaborReducePercent[userServerDataService.UserSkills.First(us => us.Skill == userRecipe
                    .Recipe.Skill).Level] ?? 1;
                ingredientCostSum += userRecipe.Recipe.Labor * userServerDataService.UserSetting!.CalorieCost / 1000 * skillReducePercent;

                var craftMinuteFee = userServerDataService.UserCraftingTables.First(u => u.CraftingTable == userRecipe.Recipe.CraftingTable).CraftMinuteFee;

                ingredientCostSum += craftMinuteFee * userRecipe.Recipe.CraftMinutes;

                foreach (var product in userElementProducts.Where(p => p.Price is null).ToList())
                {
                    // Calculate the associated User price if needed
                    var associatedUserPrice = userServerDataService.UserPrices.First(up => up.ItemOrTag == product.Element.ItemOrTag);

                    if (associatedUserPrice.OverrideIsBought)
                    {
                        continue;
                    }

                    product.Price = ingredientCostSum * product.Share / product.Element.Quantity;

                    if (debug) Console.WriteLine($"=> Product {product.Element.ItemOrTag.Name}: {product.Price}");

                    if (associatedUserPrice.Price is null)
                    {
                        // We choose the PrimaryUserElement.Price
                        if (associatedUserPrice.PrimaryUserElement == product)
                        {
                            associatedUserPrice.Price = product.Price;
                        }
                        else if (associatedUserPrice.PrimaryUserElement is null)
                        {
                            // Or we choose the min price of all related user element
                            var relatedUserElements = userServerDataService.UserElements.Where(ue => ue.Element.ItemOrTag == associatedUserPrice.ItemOrTag
                                                                                                     && ue.Element.IsProduct()
                                                                                                     && !ue.IsReintegrated).ToList();

                            if (relatedUserElements.All(ue => ue.Price is not null))
                            {
                                associatedUserPrice.Price = relatedUserElements.Min(ue => ue.Price);
                            }
                        }
                    }
                }

                nbHandled++;
            }
        } while (nbHandled > 0);

        await dbContext.SaveChangesAsync();
    }
}
