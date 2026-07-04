using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ecocraft.Services;

public class PriceCalculatorService(
    IDbContextFactory<EcoCraftDbContext> factory,
    UserElementDbService userElementDbService,
    UserPriceDbService userPriceDbService,
    UserCraftingTableDbService userCraftingTableDbService,
    CraftingTableFuelCostService craftingTableFuelCostService,
    LocalizationService localizationService,
    ILogger<PriceCalculatorService> logger)
{
    private class CalculationContext(DataContext dataContext)
    {
        public UserSetting UserSetting { get; } = dataContext.UserSettings.First();

        public Dictionary<Guid, UserPrice> UserPricesByItemOrTagId { get; } =
            dataContext.UserPrices.GroupBy(up => up.ItemOrTagId).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, UserPrice> UserPricesById { get; } =
            dataContext.UserPrices.GroupBy(up => up.Id).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, UserElement> UserElementsByElementId { get; } =
            dataContext.UserElements.GroupBy(ue => ue.ElementId).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, UserElement> UserElementsById { get; } =
            dataContext.UserElements.GroupBy(ue => ue.Id).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, (decimal? Price, decimal? MarginPrice)> OriginalUserPricesById { get; } =
            dataContext.UserPrices
                .GroupBy(up => up.Id)
                .ToDictionary(g => g.Key, g => (g.First().Price, g.First().MarginPrice));

        public Dictionary<Guid, (decimal? Price, bool IsMarginPrice)> OriginalUserElementsById { get; } =
            dataContext.UserElements
                .GroupBy(ue => ue.Id)
                .ToDictionary(g => g.Key, g => (g.First().Price, g.First().IsMarginPrice));

        public Dictionary<Guid, UserRecipe> UserRecipesByRecipeId { get; } =
            dataContext.UserRecipes.GroupBy(ur => ur.RecipeId).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, UserSkill> UserSkillsBySkillId { get; } =
            dataContext.UserSkills
                .Where(us => us.SkillId is not null)
                .GroupBy(us => us.SkillId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, UserTalent> UserTalentsByTalentId { get; } =
            dataContext.UserTalents.GroupBy(ut => ut.TalentId).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, UserCraftingTable> UserCraftingTablesByCraftingTableId { get; } =
            dataContext.UserCraftingTables.GroupBy(uct => uct.CraftingTableId).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, UserCraftingTable> UserCraftingTablesById { get; } =
            dataContext.UserCraftingTables.GroupBy(uct => uct.Id).ToDictionary(g => g.Key, g => g.First());

        public Dictionary<Guid, decimal> OriginalUserCraftingTableFeesById { get; } =
            dataContext.UserCraftingTables
                .GroupBy(uct => uct.Id)
                .ToDictionary(g => g.Key, g => g.First().TotalCraftMinuteFee);

        public Dictionary<Guid, HashSet<Guid>> ProducerSkillsByItemOrTagId { get; } = BuildProducerSkillsMap(dataContext);
        private Dictionary<Guid, decimal> DynamicValueCache { get; } = [];
        private Dictionary<Guid, decimal> RoundDynamicValueCache { get; } = [];
        private DynamicValueCalculationContext? _dynamicValueCalculationContext;
        public HashSet<Guid> DirtyUserPriceIds { get; } = [];
        public HashSet<Guid> DirtyUserElementIds { get; } = [];
        public HashSet<Guid> DirtyUserCraftingTableIds { get; } = [];

        // Real (unclamped) per-unit output cost, kept so the margin can be computed
        // on the real price even when the stored cost is clamped to the item min/max bounds.
        public Dictionary<Guid, decimal?> RealProductPriceByUserElementId { get; } = [];

        public DynamicValueCalculationContext DynamicValueCalculationContext =>
            _dynamicValueCalculationContext ??= new DynamicValueCalculationContext
            {
                UserSkillsBySkillId = UserSkillsBySkillId,
                UserTalentsByTalentId = UserTalentsByTalentId,
                UserCraftingTablesByCraftingTableId = UserCraftingTablesByCraftingTableId,
                UserRecipesByRecipeId = UserRecipesByRecipeId,
                DynamicValueCache = DynamicValueCache,
                RoundDynamicValueCache = RoundDynamicValueCache,
            };

        private static Dictionary<Guid, HashSet<Guid>> BuildProducerSkillsMap(DataContext dataContext)
        {
            var directProducerSkills = dataContext.UserElements
                .Where(ue => ue.Element is not null && ue.Element.IsProduct() && ue.UserRecipe?.Recipe?.SkillId is not null)
                .GroupBy(ue => ue.Element.ItemOrTagId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(ue => ue.UserRecipe?.Recipe?.SkillId)
                        .Where(skillId => skillId is not null)
                        .Select(skillId => skillId!.Value)
                        .ToHashSet()
                );

            var itemOrTags = dataContext.UserPrices
                .Select(up => up.ItemOrTag)
                .Concat(dataContext.UserElements.Where(ue => ue.Element is not null).Select(ue => ue.Element.ItemOrTag))
                .DistinctBy(iot => iot.Id)
                .ToList();

            var producerSkillsByItemOrTagId = new Dictionary<Guid, HashSet<Guid>>();

            foreach (var itemOrTag in itemOrTags)
            {
                var producerSkills = new HashSet<Guid>();

                foreach (var associatedId in itemOrTag.AssociatedItems.Select(ai => ai.Id).Append(itemOrTag.Id))
                {
                    if (directProducerSkills.TryGetValue(associatedId, out var directSkills))
                    {
                        producerSkills.UnionWith(directSkills);
                    }
                }

                producerSkillsByItemOrTagId[itemOrTag.Id] = producerSkills;
            }

            return producerSkillsByItemOrTagId;
        }

        public UserPrice? GetUserPrice(ItemOrTag itemOrTag)
        {
            return UserPricesByItemOrTagId.GetValueOrDefault(itemOrTag.Id);
        }

        public UserElement? GetUserElement(Element element)
        {
            return UserElementsByElementId.GetValueOrDefault(element.Id);
        }

        public IReadOnlyCollection<Guid> GetProducerSkills(ItemOrTag itemOrTag)
        {
            return ProducerSkillsByItemOrTagId.GetValueOrDefault(itemOrTag.Id) ?? [];
        }

        public bool TrySetUserElementPrice(UserElement userElement, decimal? price, bool isMarginPrice)
        {
            if (userElement.Price == price && userElement.IsMarginPrice == isMarginPrice)
            {
                return false;
            }

            userElement.Price = price;
            userElement.IsMarginPrice = isMarginPrice;
            DirtyUserElementIds.Add(userElement.Id);
            return true;
        }

        public bool TrySetUserPrice(UserPrice userPrice, decimal? price, decimal? marginPrice)
        {
            if (userPrice.Price == price && userPrice.MarginPrice == marginPrice)
            {
                return false;
            }

            userPrice.Price = price;
            userPrice.MarginPrice = marginPrice;
            DirtyUserPriceIds.Add(userPrice.Id);
            return true;
        }

        public bool HasUserPriceChangedFromOriginal(Guid userPriceId)
        {
            if (!UserPricesById.TryGetValue(userPriceId, out var userPrice)
                || !OriginalUserPricesById.TryGetValue(userPriceId, out var original))
            {
                return false;
            }

            return userPrice.Price != original.Price || userPrice.MarginPrice != original.MarginPrice;
        }

        public bool HasUserElementChangedFromOriginal(Guid userElementId)
        {
            if (!UserElementsById.TryGetValue(userElementId, out var userElement)
                || !OriginalUserElementsById.TryGetValue(userElementId, out var original))
            {
                return false;
            }

            return userElement.Price != original.Price || userElement.IsMarginPrice != original.IsMarginPrice;
        }

        public bool TrySetUserCraftingTableFee(UserCraftingTable userCraftingTable, decimal craftMinuteFee)
        {
            if (userCraftingTable.TotalCraftMinuteFee == craftMinuteFee)
            {
                return false;
            }

            userCraftingTable.TotalCraftMinuteFee = craftMinuteFee;
            DirtyUserCraftingTableIds.Add(userCraftingTable.Id);
            return true;
        }

        public bool HasUserCraftingTableFeeChangedFromOriginal(Guid userCraftingTableId)
        {
            if (!UserCraftingTablesById.TryGetValue(userCraftingTableId, out var userCraftingTable)
                || !OriginalUserCraftingTableFeesById.TryGetValue(userCraftingTableId, out var original))
            {
                return false;
            }

            return userCraftingTable.TotalCraftMinuteFee != original;
        }

    }

    public (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTags(DataContext dataContext)
    {
        var listOfProducts = dataContext.UserElements
            .Where(ue => ue.Element.IsProduct() && !ue.IsReintegrated && !(ue.Element.ItemOrTag.GetCurrentUserPrice(dataContext)?.OverrideIsBought ?? false))
            .Select(ue => ue.Element.ItemOrTag)
            .Distinct()
            .ToList();

        var listOfProductIds = listOfProducts.Select(iot => iot.Id).ToHashSet();

        var listOfIngredients = dataContext.UserElements
            .Where(ue => !listOfProductIds.Contains(ue.Element.ItemOrTag.Id))
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
        var listOfIngredientIds = listOfIngredients.Select(i => i.Id).ToHashSet();

        listOfIngredients = listOfIngredients.Where(i => !i.AssociatedTags.Any(tag => listOfIngredientIds.Contains(tag.Id)))
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

    public List<ItemOrTag> GetFuelItemOrTagsForDisplay(DataContext dataContext)
    {
        return GetFuelItemGroupsForDisplay(dataContext)
            .SelectMany(group => group.Tag is null
                ? group.Items
                : Enumerable.Repeat(group.Tag!, 1))
            .ToList();
    }

    public Dictionary<Guid, List<ItemOrTag>> GetFuelAssociatedItemsByGroupTagIdForDisplay(DataContext dataContext)
    {
        return GetFuelItemGroupsForDisplay(dataContext)
            .Where(group => group.Tag is not null)
            .ToDictionary(group => group.Tag!.Id, group => group.Items);
    }

    public async Task Calculate(DataContext dataContext, string triggerOrigin = "Unknown")
    {
        try
        {
            await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
            {
                var calculationContext = new CalculationContext(dataContext);
                var dynamicValueCalculationContext = calculationContext.DynamicValueCalculationContext;
                var marginType = calculationContext.UserSetting.MarginType;

                foreach (var userCraftingTable in dataContext.UserCraftingTables)
                {
                    var craftMinuteFee = craftingTableFuelCostService.CalculateTotalCraftMinuteFee(dataContext, userCraftingTable);
                    calculationContext.TrySetUserCraftingTableFee(userCraftingTable, craftMinuteFee);
                }

                var (_, itemOrTagsToSell) = GetCategorizedItemOrTags(dataContext);
                var itemOrTagsToSellIds = itemOrTagsToSell.Select(iot => iot.Id).ToHashSet();

                foreach (var userElement in dataContext.UserElements)
                {
                    if (userElement.Price is not null)
                    {
                        calculationContext.TrySetUserElementPrice(userElement, null, userElement.IsMarginPrice);
                    }
                }

                foreach (var userPrice in dataContext.UserPrices)
                {
                    if ((itemOrTagsToSellIds.Contains(userPrice.ItemOrTagId) || userPrice.ItemOrTag.IsTag) && !userPrice.OverrideIsBought && userPrice.Price is not null)
                    {
                        calculationContext.TrySetUserPrice(userPrice, null, userPrice.MarginPrice);
                    }

                    if (userPrice.MarginPrice is not null)
                    {
                        calculationContext.TrySetUserPrice(userPrice, userPrice.Price, null);
                    }
                }
                var remainingUserRecipes = dataContext.UserRecipes.ToList();
                var recipesWithMissingUserElements = new HashSet<Guid>();
                int nbHandled;

                do
                {
                    nbHandled = 0;
                    int iterator = 0;

                    while (remainingUserRecipes.Count > 0 && iterator < remainingUserRecipes.Count)
                    {
                        var userRecipe = remainingUserRecipes[iterator];
                        if (userRecipe.Recipe is null)
                        {
                            recipesWithMissingUserElements.Add(userRecipe.RecipeId);
                            iterator++;
                            continue;
                        }

                        var ingredientElements = userRecipe.Recipe.Elements
                            .Where(e => e.IsIngredient())
                            .ToList();

                        var userElementIngredients = ingredientElements
                            .Select(calculationContext.GetUserElement)
                            .Where(ue => ue is not null)
                            .Cast<UserElement>()
                            .ToList();

                        var productElements = userRecipe.Recipe.Elements
                            .Where(e => e.IsProduct())
                            .ToList();

                        var userElementProducts = productElements
                            .Select(calculationContext.GetUserElement)
                            .Where(ue => ue is not null)
                            .Cast<UserElement>()
                            .ToList();

                        if (userElementIngredients.Count != ingredientElements.Count || userElementProducts.Count != productElements.Count)
                        {
                            recipesWithMissingUserElements.Add(userRecipe.RecipeId);
                            iterator++;
                            continue;
                        }

                        foreach (var ingredient in userElementIngredients.Where(ue => ue.Price is null).ToList())
                        {
                            var ingredientUserPrice = calculationContext.GetUserPrice(ingredient.Element.ItemOrTag);
                            if (ingredientUserPrice is null)
                            {
                                continue;
                            }

                            if (ingredientUserPrice.Price is not null)
                            {
                                SetPriceOrMarginPrice(calculationContext, ingredient, ingredientUserPrice, userRecipe);
                                continue;
                            }

                            if (!ingredient.Element.ItemOrTag.IsTag)
                            {
                                continue;
                            }

                            if (ingredientUserPrice.PrimaryUserPrice?.Price is not null)
                            {
                                calculationContext.TrySetUserPrice(ingredientUserPrice, ingredientUserPrice.PrimaryUserPrice.Price, ingredientUserPrice.PrimaryUserPrice.MarginPrice);
                                SetPriceOrMarginPrice(calculationContext, ingredient, ingredientUserPrice, userRecipe);
                                continue;
                            }

                            var associatedItemsUserPrices = ingredient.Element.ItemOrTag.AssociatedItems
                                .Select(calculationContext.GetUserPrice)
                                .Where(up => up is not null)
                                .Cast<UserPrice>()
                                .ToList();

                            if (associatedItemsUserPrices.Count == 0 || !associatedItemsUserPrices.All(up => up.Price is not null))
                            {
                                continue;
                            }

                            var cheapest = associatedItemsUserPrices.MinBy(up => up.Price)!;
                            calculationContext.TrySetUserPrice(ingredientUserPrice, cheapest.Price, cheapest.MarginPrice);
                            SetPriceOrMarginPrice(calculationContext, ingredient, ingredientUserPrice, userRecipe);
                        }

                        var reintegratedProducts = userElementProducts.Where(ue => ue.IsReintegrated).ToList();

                        foreach (var reintegratedProduct in reintegratedProducts)
                        {
                            var reintegratedUserPrice = calculationContext.GetUserPrice(reintegratedProduct.Element.ItemOrTag);
                            if (reintegratedUserPrice is null)
                            {
                                continue;
                            }

                            SetPriceOrMarginPrice(calculationContext, reintegratedProduct, reintegratedUserPrice, userRecipe);
                            if (reintegratedProduct.Price is not null)
                            {
                                calculationContext.TrySetUserElementPrice(reintegratedProduct, reintegratedProduct.Price * -1, reintegratedProduct.IsMarginPrice);
                            }
                        }

                        if (userElementIngredients.Any(ue => ue.Price is null))
                        {
                            iterator++;
                            continue;
                        }

                        if (reintegratedProducts.Any(ue =>
                            calculationContext.GetUserPrice(ue.Element.ItemOrTag) is { Price: null }))
                        {
                            iterator++;
                            continue;
                        }

                        remainingUserRecipes.RemoveAt(iterator);

                        var ingredientCostSum = -1 * userElementIngredients.Sum(ue =>
                            (ue.Price ?? 0m) * ue.Element.Quantity.GetRoundFactorDynamicValue(dataContext, dynamicValueCalculationContext));
                        ingredientCostSum += reintegratedProducts.Sum(ue =>
                            (ue.Price ?? 0m) * ue.Element.Quantity.GetRoundFactorDynamicValue(dataContext, dynamicValueCalculationContext));

                        var laborCost = userRecipe.Recipe.Labor.GetDynamicValue(dataContext, dynamicValueCalculationContext)
                                        * calculationContext.UserSetting.CalorieCost
                                        / 1000;
                        ingredientCostSum += laborCost;

                        if (calculationContext.UserCraftingTablesByCraftingTableId.TryGetValue(userRecipe.Recipe.CraftingTableId, out var currentUserCraftingTable))
                        {
                            var craftMinuteFee = craftingTableFuelCostService.CalculateTotalCraftMinuteFee(dataContext, currentUserCraftingTable);
                            calculationContext.TrySetUserCraftingTableFee(currentUserCraftingTable, craftMinuteFee);
                            var craftMinutes = userRecipe.Recipe.GetEffectiveCraftMinutes(dataContext, userRecipe, dynamicValueCalculationContext);
                            ingredientCostSum += currentUserCraftingTable.TotalCraftMinuteFee * craftMinutes;
                        }

                        foreach (var product in userElementProducts.Where(p => p.Price is null).ToList())
                        {
                            var finalQuantity = product.Element.Quantity.GetRoundFactorDynamicValue(dataContext, dynamicValueCalculationContext);
                            if (finalQuantity == 0)
                            {
                                continue;
                            }

                            // Real (unclamped) output cost. The stored cost is clamped to the
                            // produced item min/max bounds, but the margin must be computed on the
                            // real price (then clamped in turn) — see issue #84.
                            var realProductPrice = ingredientCostSum * product.Share / finalQuantity;
                            var clampedProductPrice = ClampToItemBounds(realProductPrice, product.Element.ItemOrTag);
                            calculationContext.RealProductPriceByUserElementId[product.Id] = realProductPrice;
                            calculationContext.TrySetUserElementPrice(product, clampedProductPrice, product.IsMarginPrice);

                            var productUserPrice = calculationContext.GetUserPrice(product.Element.ItemOrTag);
                            if (productUserPrice is null || productUserPrice.OverrideIsBought || productUserPrice.Price is not null)
                            {
                                continue;
                            }

                            if (productUserPrice.PrimaryUserElement == product)
                            {
                                SetUserPriceWithMargin(calculationContext, productUserPrice, product.Price, realProductPrice, product.Element.ItemOrTag, marginType);
                            }
                            else if (productUserPrice.PrimaryUserElement is null)
                            {
                                var relatedUserElements = product.Element.ItemOrTag.Elements
                                    .Where(e => e.IsProduct())
                                    .Select(calculationContext.GetUserElement)
                                    .Where(ue => ue is not null && !ue.IsReintegrated)
                                    .Cast<UserElement>()
                                    .ToList();

                                if (relatedUserElements.All(ue => ue.Price is not null))
                                {
                                    // Clamping shares the same bounds for every producer of this item, so the
                                    // cheapest stored (clamped) cost is also the cheapest real cost.
                                    var cheapest = relatedUserElements.OrderBy(ue => ue.Price).First();
                                    var cheapestRealPrice = calculationContext.RealProductPriceByUserElementId.GetValueOrDefault(cheapest.Id) ?? cheapest.Price;
                                    SetUserPriceWithMargin(calculationContext, productUserPrice, cheapest.Price, cheapestRealPrice, product.Element.ItemOrTag, marginType);
                                }
                            }
                        }

                        nbHandled++;
                    }
                } while (nbHandled > 0);

                CalculateFuelTagPrices(calculationContext, dataContext);

                if (recipesWithMissingUserElements.Count > 0)
                {
                    logger.LogWarning(
                        "Price calculation skipped recipes with missing UserElements. trigger={TriggerOrigin} missingRecipeCount={MissingRecipeCount} sampleRecipeIds={SampleRecipeIds}",
                        triggerOrigin,
                        recipesWithMissingUserElements.Count,
                        string.Join(", ", recipesWithMissingUserElements.Take(10)));
                }
                var finalDirtyUserPriceIds = calculationContext.DirtyUserPriceIds
                    .Where(calculationContext.HasUserPriceChangedFromOriginal)
                    .ToList();
                var finalDirtyUserElementIds = calculationContext.DirtyUserElementIds
                    .Where(calculationContext.HasUserElementChangedFromOriginal)
                    .ToList();
                var finalDirtyUserCraftingTableIds = calculationContext.DirtyUserCraftingTableIds
                    .Where(calculationContext.HasUserCraftingTableFeeChangedFromOriginal)
                    .ToList();

                var existingDirtyUserPriceIds = await context.UserPrices
                    .Where(up => finalDirtyUserPriceIds.Contains(up.Id))
                    .Select(up => up.Id)
                    .ToHashSetAsync();

                var existingDirtyUserElementIds = await context.UserElements
                    .Where(ue => finalDirtyUserElementIds.Contains(ue.Id))
                    .Select(ue => ue.Id)
                    .ToHashSetAsync();

                var existingDirtyUserCraftingTableIds = await context.UserCraftingTables
                    .Where(uct => finalDirtyUserCraftingTableIds.Contains(uct.Id))
                    .Select(uct => uct.Id)
                    .ToHashSetAsync();

                foreach (var userPriceId in finalDirtyUserPriceIds)
                {
                    if (!existingDirtyUserPriceIds.Contains(userPriceId))
                    {
                        continue;
                    }

                    if (calculationContext.UserPricesById.TryGetValue(userPriceId, out var userPrice))
                    {
                        userPriceDbService.UpdateCalculatedPrices(context, userPrice);
                    }
                }

                foreach (var userElementId in finalDirtyUserElementIds)
                {
                    if (!existingDirtyUserElementIds.Contains(userElementId))
                    {
                        continue;
                    }

                    if (calculationContext.UserElementsById.TryGetValue(userElementId, out var userElement))
                    {
                        userElementDbService.UpdateAll(context, userElement);
                    }
                }

                foreach (var userCraftingTableId in finalDirtyUserCraftingTableIds)
                {
                    if (!existingDirtyUserCraftingTableIds.Contains(userCraftingTableId))
                    {
                        continue;
                    }

                    if (calculationContext.UserCraftingTablesById.TryGetValue(userCraftingTableId, out var userCraftingTable))
                    {
                        userCraftingTableDbService.UpdateTotalCraftMinuteFee(context, userCraftingTable);
                    }
                }
                return;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Price calculation failed for trigger {TriggerOrigin}. skills={UserSkills}, recipes={UserRecipes}, elements={UserElements}, prices={UserPrices}",
                triggerOrigin,
                dataContext.UserSkills.Count,
                dataContext.UserRecipes.Count,
                dataContext.UserElements.Count,
                dataContext.UserPrices.Count);
            throw;
        }
    }

    private void CalculateFuelTagPrices(CalculationContext calculationContext, DataContext dataContext)
    {
        foreach (var fuelGroup in GetFuelItemGroupsForDisplay(dataContext).Where(group => group.Tag is not null))
        {
            var tagUserPrice = calculationContext.GetUserPrice(fuelGroup.Tag!);
            if (tagUserPrice is null || tagUserPrice.OverrideIsBought)
            {
                continue;
            }

            if (tagUserPrice.PrimaryUserPrice?.Price is not null)
            {
                calculationContext.TrySetUserPrice(tagUserPrice, tagUserPrice.PrimaryUserPrice.Price, tagUserPrice.PrimaryUserPrice.MarginPrice);
                continue;
            }

            var associatedFuelPrices = fuelGroup.Items
                .Select(calculationContext.GetUserPrice)
                .Where(userPrice => userPrice?.ItemOrTag.FuelCalories is > 0)
                .Cast<UserPrice>()
                .ToList();

            if (associatedFuelPrices.Count != fuelGroup.Items.Count
                || associatedFuelPrices.Any(userPrice => userPrice.Price is null))
            {
                calculationContext.TrySetUserPrice(tagUserPrice, null, null);
                continue;
            }

            var cheapestFuel = associatedFuelPrices
                .MinBy(userPrice => userPrice.Price!.Value / userPrice.ItemOrTag.FuelCalories!.Value)!;
            calculationContext.TrySetUserPrice(tagUserPrice, cheapestFuel.Price, cheapestFuel.MarginPrice);
        }
    }

    private List<ItemOrTag> GetPotentialFuelItemsAndTags(DataContext dataContext)
    {
        return dataContext.UserCraftingTables
            .SelectMany(uct => craftingTableFuelCostService.GetEligibleFuelItemsAndTags(uct.CraftingTable))
            .DistinctBy(item => item.Id)
            .ToList();
    }

    private List<FuelItemGroup> GetFuelItemGroupsForDisplay(DataContext dataContext)
    {
        var fuelItems = GetPotentialFuelItemsAndTags(dataContext)
            .Where(itemOrTag => !itemOrTag.IsTag)
            .DistinctBy(itemOrTag => itemOrTag.Id)
            .ToList();

        return fuelItems
            .GroupBy(item => craftingTableFuelCostService.GetFuelGroupingTag(item, fuelItems))
            .Select(group => new FuelItemGroup(
                group.Key,
                group
                    .OrderBy(localizationService.GetTranslation)
                    .ToList()))
            .Where(group => group.Items.Count > 0)
            .OrderBy(group => group.Tag is null ? 1 : 0)
            .ThenBy(group => group.Tag is null
                ? localizationService.GetTranslation(group.Items.First())
                : localizationService.GetTranslation(group.Tag))
            .ToList();
    }

    private sealed record FuelItemGroup(ItemOrTag? Tag, List<ItemOrTag> Items);

    // storedPrice is the cost stored on the UserPrice (already clamped to the item bounds).
    // marginBasePrice is the real, unclamped cost the margin is computed from; the resulting
    // margin is then clamped to the same item bounds (issue #84).
    private static void SetUserPriceWithMargin(CalculationContext calculationContext, UserPrice userPrice, decimal? storedPrice, decimal? marginBasePrice, ItemOrTag clampItemOrTag, MarginType marginType)
    {
        decimal? marginPrice;

        if (marginBasePrice is null || userPrice.UserMargin is null)
        {
            marginPrice = null;
        }
        else
        {
            marginPrice = userPrice.MarginPrice;

            switch (marginType)
            {
                case MarginType.MarkUp:
                    marginPrice = marginBasePrice * (1 + userPrice.UserMargin.Margin / 100);
                    break;
                case MarginType.GrossMargin:
                {
                    var divisionFactor = 1 - userPrice.UserMargin.Margin / 100;
                    if (divisionFactor > 0)
                    {
                        marginPrice = marginBasePrice / divisionFactor;
                    }
                    break;
                }
            }

            if (marginPrice is not null)
            {
                marginPrice = ClampToItemBounds(marginPrice.Value, clampItemOrTag);
            }

            if (marginPrice is not null && userPrice.UserMargin.Rounding != RoundingMode.None)
            {
                marginPrice = SmartRound(marginPrice.Value, userPrice.UserMargin.Rounding);
            }
        }

        calculationContext.TrySetUserPrice(userPrice, storedPrice, marginPrice);
    }

    // Bounds a price to the item min/max defined by the server admin. No-op when a bound is null.
    private static decimal ClampToItemBounds(decimal price, ItemOrTag itemOrTag)
    {
        if (itemOrTag.MinPrice is not null && price < itemOrTag.MinPrice.Value)
        {
            price = itemOrTag.MinPrice.Value;
        }

        if (itemOrTag.MaxPrice is not null && price > itemOrTag.MaxPrice.Value)
        {
            price = itemOrTag.MaxPrice.Value;
        }

        return price;
    }

    // Below 1$ → no rounding. Above:
    //   < 2$    → step 0.25 (marketing keeps the .25 grid as-is, .99 trick doesn't apply here)
    //   < 5$    → step 0.5
    //   < 100$  → step 1
    //   < 1000$ → step 10
    //   ≥ 1000$ → step 100
    // Up = ceiling, Down = floor, Marketing = ceiling then -0.01 (except the 0.25 tier).
    private static decimal SmartRound(decimal value, RoundingMode mode)
    {
        if (value < 1m) return value;

        decimal step;
        bool marketingTrick;
        if (value < 2m) { step = 0.25m; marketingTrick = false; }
        else if (value < 5m) { step = 0.5m; marketingTrick = true; }
        else if (value < 100m) { step = 1m; marketingTrick = true; }
        else if (value < 1000m) { step = 10m; marketingTrick = true; }
        else { step = 100m; marketingTrick = true; }

        var divided = value / step;
        if (mode == RoundingMode.Down) return Math.Floor(divided) * step;

        var rounded = Math.Ceiling(divided) * step;
        if (mode == RoundingMode.Marketing && marketingTrick) rounded -= 0.01m;
        return rounded;
    }

    private static void SetPriceOrMarginPrice(CalculationContext calculationContext, UserElement ingredient, UserPrice userPrice, UserRecipe userRecipe)
    {
        var producerSkills = calculationContext.GetProducerSkills(ingredient.Element.ItemOrTag);
        var currentRecipeSkillId = userRecipe.Recipe.SkillId;

        Guid? primarySkillId = null;
        var hasPrimarySelection = userPrice.PrimaryUserElement is not null || userPrice.PrimaryUserPrice is not null;

        if (userPrice.PrimaryUserElement is not null)
        {
            primarySkillId = userPrice.PrimaryUserElement.Element.Recipe.SkillId;
        }
        else if (userPrice.PrimaryUserPrice is not null)
        {
            primarySkillId = userPrice.PrimaryUserPrice.PrimaryUserElement?.Element.Recipe.SkillId;

            if (primarySkillId is null)
            {
                var selectedPrimaryItemProducer = userPrice.PrimaryUserPrice.ItemOrTag.Elements
                    .Where(e => e.IsProduct())
                    .Select(calculationContext.GetUserElement)
                    .Where(ue => ue is not null && !ue.IsReintegrated && ue.Price is not null)
                    .Cast<UserElement>()
                    .MinBy(ue => ue.Price);

                primarySkillId = selectedPrimaryItemProducer?.Element.Recipe.SkillId;
            }
        }

        var hasCrossSkillRecipe = producerSkills.Any(skillId => skillId != currentRecipeSkillId);
        if (hasPrimarySelection)
        {
            hasCrossSkillRecipe = primarySkillId != currentRecipeSkillId;
        }

        if (calculationContext.UserSetting.ApplyMarginBetweenSkills
            && userPrice is { OverrideIsBought: false, MarginPrice: not null }
            && producerSkills.Count > 0
            && hasCrossSkillRecipe)
        {
            calculationContext.TrySetUserElementPrice(ingredient, userPrice.MarginPrice, true);
            return;
        }

        calculationContext.TrySetUserElementPrice(ingredient, userPrice.Price, false);
    }
}
