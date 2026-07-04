using ecocraft.Models;

namespace ecocraft.Services;

/// <summary>
/// Centralizes crafting-table fuel rules derived from Eco server data.
/// The price calculator uses it for cost/minute, and shopping-list flows can reuse the same
/// consumption calculation to determine how much fuel must be bought for a crafting plan.
/// </summary>
public class CraftingTableFuelCostService
{
    public ItemOrTag? GetCraftingTableItem(CraftingTable craftingTable)
    {
        var server = GetServer(craftingTable);
        return server?.ItemOrTags.FirstOrDefault(item => !item.IsTag && item.Name == craftingTable.Name);
    }

    public bool HasFuelConsumption(CraftingTable craftingTable)
    {
        return GetCraftingTableItem(craftingTable)?.FuelConsumptionPerSecond is > 0;
    }

    public IReadOnlyList<ItemOrTag> GetEligibleFuelItems(CraftingTable craftingTable)
    {
        var craftingTableItem = GetCraftingTableItem(craftingTable);
        var acceptedFuelTagNames = GetAcceptedFuelTagNames(craftingTableItem);

        if (acceptedFuelTagNames.Count == 0)
        {
            return [];
        }

        var server = GetServer(craftingTable);
        if (server is null)
        {
            return [];
        }

        return server.ItemOrTags
            .Where(item => !item.IsTag
                           && item.FuelCalories is > 0
                           && item.AssociatedTags.Any(tag => acceptedFuelTagNames.Contains(tag.Name)))
            .OrderBy(item => item.Name)
            .ToList();
    }

    public IReadOnlyList<ItemOrTag> GetEligibleFuelTags(CraftingTable craftingTable)
    {
        var craftingTableItem = GetCraftingTableItem(craftingTable);
        var acceptedFuelTagNames = GetAcceptedFuelTagNames(craftingTableItem);

        if (acceptedFuelTagNames.Count == 0)
        {
            return [];
        }

        var server = GetServer(craftingTable);
        if (server is null)
        {
            return [];
        }

        var fuelItemIds = GetEligibleFuelItems(craftingTable)
            .Select(item => item.Id)
            .ToHashSet();

        return server.ItemOrTags
            .Where(item => item.IsTag
                           && acceptedFuelTagNames.Contains(item.Name)
                           && item.AssociatedItems.Any(associatedItem => fuelItemIds.Contains(associatedItem.Id)))
            .OrderBy(item => item.Name)
            .ToList();
    }

    public IReadOnlyList<ItemOrTag> GetEligibleFuelItemsAndTags(CraftingTable craftingTable)
    {
        var craftingTableItem = GetCraftingTableItem(craftingTable);
        var acceptedFuelTagNames = GetAcceptedFuelTagNames(craftingTableItem);

        if (acceptedFuelTagNames.Count == 0)
        {
            return [];
        }

        var server = GetServer(craftingTable);
        if (server is null)
        {
            return [];
        }

        var fuelItems = GetEligibleFuelItems(craftingTable);
        var fuelTags = GetEligibleFuelTags(craftingTable);

        return fuelTags
            .Concat(fuelItems)
            .DistinctBy(item => item.Id)
            .ToList();
    }

    public ItemOrTag? GetFuelGroupingTag(ItemOrTag fuelItem, IReadOnlyCollection<ItemOrTag> eligibleFuelItems)
    {
        if (fuelItem.IsTag)
        {
            return fuelItem;
        }

        var eligibleFuelItemIds = eligibleFuelItems
            .Where(item => !item.IsTag)
            .Select(item => item.Id)
            .ToHashSet();
        var eligibleFuelItemCount = eligibleFuelItemIds.Count;

        if (eligibleFuelItemCount <= 1)
        {
            return null;
        }

        return fuelItem.AssociatedTags
            .Select(tag =>
            {
                var matchingFuelCount = tag.AssociatedItems.Count(item => eligibleFuelItemIds.Contains(item.Id));
                var associatedItemCount = tag.AssociatedItems.Count;

                return new
                {
                    Tag = tag,
                    MatchingFuelCount = matchingFuelCount,
                    AssociatedItemCount = associatedItemCount,
                    MatchingFuelRatio = associatedItemCount == 0
                        ? 0m
                        : (decimal)matchingFuelCount / associatedItemCount,
                };
            })
            .Where(candidate => candidate.MatchingFuelCount > 1
                                && (candidate.MatchingFuelCount < eligibleFuelItemCount || eligibleFuelItemCount <= 2)
                                && candidate.AssociatedItemCount > 0
                                && candidate.MatchingFuelRatio == 1m)
            .OrderByDescending(candidate => candidate.MatchingFuelRatio)
            .ThenByDescending(candidate => candidate.MatchingFuelCount)
            .ThenBy(candidate => candidate.Tag.Name)
            .Select(candidate => candidate.Tag)
            .FirstOrDefault();
    }

    public decimal CalculateFuelCraftMinuteFee(DataContext dataContext, UserCraftingTable userCraftingTable)
    {
        var fuelItem = ResolveFuelItem(userCraftingTable);

        if (fuelItem?.FuelCalories is not > 0
            || !IsEligibleFuel(userCraftingTable.CraftingTable, fuelItem))
        {
            return 0m;
        }

        var fuelPrice = fuelItem.GetCurrentUserPrice(dataContext)?.Price;
        if (fuelPrice is null)
        {
            return 0m;
        }

        var craftingTableItem = GetCraftingTableItem(userCraftingTable.CraftingTable);
        return CalculateFuelCraftMinuteFee(
            craftingTableItem?.FuelConsumptionPerSecond,
            fuelItem.FuelCalories,
            fuelPrice.Value);
    }

    public decimal CalculateTotalCraftMinuteFee(DataContext dataContext, UserCraftingTable userCraftingTable)
    {
        return userCraftingTable.AdditionalCraftMinuteFee + CalculateFuelCraftMinuteFee(dataContext, userCraftingTable);
    }

    /// <summary>
    /// Returns the number of fuel items consumed for a craft duration.
    /// This keeps quantity calculation independent from prices so it can be reused by shopping lists.
    /// </summary>
    public decimal CalculateFuelQuantityForCraftMinutes(CraftingTable craftingTable, ItemOrTag fuelItem, decimal craftMinutes)
    {
        var craftingTableItem = GetCraftingTableItem(craftingTable);

        if (craftMinutes <= 0
            || fuelItem.FuelCalories is not > 0
            || craftingTableItem?.FuelConsumptionPerSecond is not > 0)
        {
            return 0m;
        }

        var energyConsumed = craftingTableItem.FuelConsumptionPerSecond.Value * 60m * craftMinutes;
        return energyConsumed / fuelItem.FuelCalories.Value;
    }

    public static decimal CalculateFuelCraftMinuteFee(decimal? fuelConsumptionPerSecond, decimal? fuelCalories, decimal? fuelPrice)
    {
        if (fuelConsumptionPerSecond is not > 0
            || fuelCalories is not > 0
            || fuelPrice is not > 0)
        {
            return 0m;
        }

        return fuelConsumptionPerSecond.Value * 60m / fuelCalories.Value * fuelPrice.Value;
    }

    public bool IsEligibleFuel(CraftingTable craftingTable, ItemOrTag fuelItem)
    {
        return GetEligibleFuelItems(craftingTable).Any(item => item.Id == fuelItem.Id);
    }

    private static Server? GetServer(CraftingTable craftingTable)
    {
        return craftingTable.Server ?? craftingTable.Recipes.FirstOrDefault()?.Server;
    }

    private static HashSet<string> GetAcceptedFuelTagNames(ItemOrTag? craftingTableItem)
    {
        return craftingTableItem?.AcceptedFuelTags?.ToHashSet(StringComparer.Ordinal) ?? [];
    }

    private ItemOrTag? ResolveFuelItem(UserCraftingTable userCraftingTable)
    {
        if (userCraftingTable.FuelItem is not null)
        {
            return userCraftingTable.FuelItem;
        }

        return userCraftingTable.FuelItemId is Guid fuelItemId
            ? GetEligibleFuelItems(userCraftingTable.CraftingTable).FirstOrDefault(item => item.Id == fuelItemId)
            : null;
    }
}
