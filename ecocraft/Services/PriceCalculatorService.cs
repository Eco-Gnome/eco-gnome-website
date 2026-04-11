using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

public class PriceCalculatorService(
    IDbContextFactory<EcoCraftDbContext> factory,
    UserElementDbService userElementDbService,
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
        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            // Reset Prices
            var (_, itemOrTagsToSell) = GetCategorizedItemOrTags(dataContext);

            dataContext.UserRecipes.ForEach(ur => ur.UserElements.ForEach(ue => ue.Price = null));
            dataContext.UserPrices
                .Where(up => (itemOrTagsToSell.Contains(up.ItemOrTag) || up.ItemOrTag.IsTag) && !up.OverrideIsBought)
                .ToList()
                .ForEach(up => up.Price = null);

            dataContext.UserPrices.ForEach(up => up.MarginPrice = null);

            var marginType = dataContext.UserSettings.First().MarginType;
            List<UserRecipe> remainingUserRecipes = dataContext.UserRecipes.ToList();
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
                            SetPriceOrMarginPrice(context, dataContext, ingredient, ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);
                            if (debug) Console.WriteLine($"=> Ingredient {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                            continue;
                        }

                        if (!ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.ItemOrTag.IsTag) continue;

                        if (ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserPrice?.Price is not null)
                        {
                            ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price = ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserPrice!.Price;
                            ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.MarginPrice = ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.PrimaryUserPrice!.MarginPrice;

                            SetPriceOrMarginPrice(context, dataContext, ingredient, ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);
                            if (debug) Console.WriteLine($"=> Ingredient Tag (from primary) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");

                            continue;
                        }

                        var associatedItemsUserPrices = ingredient.Element.ItemOrTag.AssociatedItems
                            .Select(iot => iot.GetCurrentUserPrice(dataContext)!)
                            .ToList();

                        if (associatedItemsUserPrices.Count == 0 || !associatedItemsUserPrices.All(up => up.Price is not null)) continue;

                        var cheapest = associatedItemsUserPrices.MinBy(up => up.Price)!;
                        ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.Price = cheapest.Price;
                        ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!.MarginPrice = cheapest.MarginPrice;
                        SetPriceOrMarginPrice(context, dataContext, ingredient, ingredient.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);

                        if (debug) Console.WriteLine($"=> Ingredient Tag (from Min) {ingredient.Element.ItemOrTag.Name}: {ingredient.Price}");
                    }

                    var userElementProducts = userRecipe.Recipe.Elements.Where(e => e.IsProduct()).Select(e => e.GetCurrentUserElement(dataContext)!).ToList();
                    var reintegratedProducts = userElementProducts.Where(ue => ue.IsReintegrated).ToList();

                    // We calculate the element price of reintegrated products
                    foreach (var reintegratedProduct in reintegratedProducts)
                    {
                        SetPriceOrMarginPrice(context, dataContext, reintegratedProduct, reintegratedProduct.Element.ItemOrTag.GetCurrentUserPrice(dataContext)!, userRecipe);
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
                    ingredientCostSum += userRecipe.Recipe.Labor.GetDynamicValue(dataContext) * dataContext.UserSettings.First().CalorieCost / 1000;
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

            return Task.CompletedTask;
        });
    }

    private void SetPriceOrMarginPrice(EcoCraftDbContext context, DataContext dataContext, UserElement ingredient, UserPrice userPrice, UserRecipe userRecipe)
    {
        var recipesThatProduceMyIngredient = ingredient.Element.ItemOrTag.GetAssociatedItemsAndSelf().SelectMany(i => i.Elements)
            .Where(e => e.IsProduct())
            .Select(e => e.GetCurrentUserElement(dataContext))
            .Where(ue => ue is not null)
            .Select(ue => ue!.UserRecipe.Recipe)
            .Where(r => r.Skill is not null)
            .ToList();

        Guid? primarySkillId = null;
        var hasPrimarySelection = userPrice.PrimaryUserElement is not null || userPrice.PrimaryUserPrice is not null;

        if (userPrice.PrimaryUserElement is not null)
        {
            primarySkillId = userPrice.PrimaryUserElement.Element.Recipe.SkillId;
        }
        else if (userPrice.PrimaryUserPrice is not null)
        {
            primarySkillId = userPrice.PrimaryUserPrice.PrimaryUserElement?.Element.Recipe.SkillId;

            // If only a primary item is selected (tag case), infer the recipe skill currently used by that item.
            if (primarySkillId is null)
            {
                var selectedPrimaryItemProducer = userPrice.PrimaryUserPrice.ItemOrTag.Elements
                    .Where(e => e.IsProduct())
                    .Select(e => e.GetCurrentUserElement(dataContext))
                    .Where(ue => ue is not null && !ue.IsReintegrated && ue.Price is not null)
                    .MinBy(ue => ue!.Price);

                primarySkillId = selectedPrimaryItemProducer?.Element.Recipe.SkillId;
            }
        }
        var hasCrossSkillRecipe = recipesThatProduceMyIngredient.Any(r => r.SkillId != userRecipe.Recipe.SkillId);

        if (hasPrimarySelection)
        {
            hasCrossSkillRecipe = primarySkillId != userRecipe.Recipe.SkillId;
        }

        if (dataContext.UserSettings.First().ApplyMarginBetweenSkills
            && userPrice is { OverrideIsBought: false, MarginPrice: not null }
            && recipesThatProduceMyIngredient.Count > 0
            && hasCrossSkillRecipe)
        {
            ingredient.Price = userPrice.MarginPrice;
            ingredient.IsMarginPrice = true;
        }
        else
        {
            ingredient.Price = userPrice.Price;
            ingredient.IsMarginPrice = false;
        }

        userElementDbService.UpdateAll(context, ingredient);
    }
}
