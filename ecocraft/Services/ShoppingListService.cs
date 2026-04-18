using ecocraft.Models;

namespace ecocraft.Services;

public class ShoppingListService
{
    private const decimal Epsilon = 0.000001m;

    public Dictionary<ItemOrTag, decimal> GetAggregatedOutputs(DataContext shoppingList, List<UserRecipe> shoppingListRecipes, Dictionary<ItemOrTag, decimal>? outputs = null, int depth = 0)
    {
        outputs ??= new Dictionary<ItemOrTag, decimal>(ItemOrTagIdComparer.Instance);

        foreach (var shoppingListRecipe in shoppingListRecipes)
        {
            foreach (var userElement in shoppingListRecipe.Recipe.Elements.OrderBy(e => (e.IsProduct() ? 0 : 1000) + e.Index))
            {
                var currentItemOrTag = userElement.ItemOrTag;
                var quantity = userElement.Quantity.GetDynamicValue(shoppingList) * shoppingListRecipe.RoundFactor;
                ApplyQuantity(outputs, currentItemOrTag, quantity);
            }

            if (shoppingListRecipe.ChildrenUserRecipes.Count > 0)
            {
                GetAggregatedOutputs(shoppingList, shoppingListRecipe.ChildrenUserRecipes, outputs, depth + 4);
            }
        }

        return outputs;
    }

    private static void ApplyQuantity(Dictionary<ItemOrTag, decimal> outputs, ItemOrTag currentItemOrTag, decimal quantity)
    {
        if (Math.Abs(quantity) <= Epsilon)
        {
            return;
        }

        if (quantity > 0)
        {
            var remainingSupply = quantity;
            var compatibleNeeds = outputs
                .Where(kvp => kvp.Value < -Epsilon && CanSupply(kvpKey: currentItemOrTag, demand: kvp.Key))
                .Select(kvp => kvp.Key)
                .OrderBy(key => key.Id == currentItemOrTag.Id ? 0 : 1)
                .ThenBy(key => key.IsTag ? 1 : 0)
                .ToList();

            foreach (var needKey in compatibleNeeds)
            {
                if (!outputs.TryGetValue(needKey, out var needValue) || needValue >= -Epsilon)
                {
                    continue;
                }

                var consumed = Math.Min(remainingSupply, -needValue);
                needValue += consumed;
                remainingSupply -= consumed;

                if (Math.Abs(needValue) <= Epsilon)
                {
                    outputs.Remove(needKey);
                }
                else
                {
                    outputs[needKey] = needValue;
                }

                if (remainingSupply <= Epsilon)
                {
                    break;
                }
            }

            if (remainingSupply > Epsilon)
            {
                AddOrUpdate(outputs, currentItemOrTag, remainingSupply);
            }

            return;
        }

        var remainingNeed = -quantity;
        var compatibleSupplies = outputs
            .Where(kvp => kvp.Value > Epsilon && CanSupply(kvpKey: kvp.Key, demand: currentItemOrTag))
            .Select(kvp => kvp.Key)
            .OrderBy(key => key.Id == currentItemOrTag.Id ? 0 : 1)
            .ThenBy(key => key.IsTag ? 1 : 0)
            .ToList();

        foreach (var supplyKey in compatibleSupplies)
        {
            if (!outputs.TryGetValue(supplyKey, out var supplyValue) || supplyValue <= Epsilon)
            {
                continue;
            }

            var consumed = Math.Min(remainingNeed, supplyValue);
            supplyValue -= consumed;
            remainingNeed -= consumed;

            if (Math.Abs(supplyValue) <= Epsilon)
            {
                outputs.Remove(supplyKey);
            }
            else
            {
                outputs[supplyKey] = supplyValue;
            }

            if (remainingNeed <= Epsilon)
            {
                break;
            }
        }

        if (remainingNeed > Epsilon)
        {
            AddOrUpdate(outputs, currentItemOrTag, -remainingNeed);
        }
    }

    private static bool CanSupply(ItemOrTag kvpKey, ItemOrTag demand)
    {
        return kvpKey.GetAssociatedTagsAndSelf().Any(iot => iot.Id == demand.Id);
    }

    private static void AddOrUpdate(Dictionary<ItemOrTag, decimal> outputs, ItemOrTag key, decimal delta)
    {
        if (Math.Abs(delta) <= Epsilon)
        {
            return;
        }

        var next = outputs.GetValueOrDefault(key, 0m) + delta;
        if (Math.Abs(next) <= Epsilon)
        {
            outputs.Remove(key);
        }
        else
        {
            outputs[key] = next;
        }
    }

    private sealed class ItemOrTagIdComparer : IEqualityComparer<ItemOrTag>
    {
        public static ItemOrTagIdComparer Instance { get; } = new();

        public bool Equals(ItemOrTag? x, ItemOrTag? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Id == y.Id;
        }

        public int GetHashCode(ItemOrTag obj)
        {
            return obj.Id.GetHashCode();
        }
    }
}
