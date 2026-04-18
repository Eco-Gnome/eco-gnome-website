using ecocraft.Models;

namespace ecocraft.Services;

public static class ShoppingListCoverageCalculator
{
    private const decimal Epsilon = 0.000001m;

    public sealed class IngredientCoverageSnapshot
    {
        private readonly Dictionary<Guid, (decimal RequiredQuantity, decimal CoveredQuantity)> _coverageByIngredientId;

        internal IngredientCoverageSnapshot(Dictionary<Guid, (decimal RequiredQuantity, decimal CoveredQuantity)> coverageByIngredientId)
        {
            _coverageByIngredientId = coverageByIngredientId;
        }

        public decimal GetRequiredQuantity(Element ingredient)
        {
            return _coverageByIngredientId.TryGetValue(ingredient.Id, out var coverage)
                ? coverage.RequiredQuantity
                : 0m;
        }

        public decimal GetCoveredQuantity(Element ingredient)
        {
            return _coverageByIngredientId.TryGetValue(ingredient.Id, out var coverage)
                ? coverage.CoveredQuantity
                : 0m;
        }

        public decimal GetMissingQuantity(Element ingredient)
        {
            if (!_coverageByIngredientId.TryGetValue(ingredient.Id, out var coverage))
            {
                return 0m;
            }

            return Math.Max(coverage.RequiredQuantity - coverage.CoveredQuantity, 0m);
        }

        public bool IsFulfilled(Element ingredient)
        {
            if (!_coverageByIngredientId.TryGetValue(ingredient.Id, out var coverage))
            {
                return true;
            }

            return coverage.CoveredQuantity + Epsilon >= coverage.RequiredQuantity;
        }
    }

    private readonly record struct IngredientDemand(Guid IngredientElementId, Guid ItemOrTagId, decimal RequiredQuantity);
    private readonly record struct ProductSupply(HashSet<Guid> SupportedIngredientItemOrTagIds, decimal QuantityPerCraft);

    public static IngredientCoverageSnapshot ComputeCoverage(UserRecipe parentRecipe, DataContext shoppingList, IEnumerable<UserRecipe> children)
    {
        var demands = parentRecipe.Recipe.Elements
            .Where(e => e.IsIngredient())
            .OrderBy(e => e.Index)
            .Select(e => new IngredientDemand(
                IngredientElementId: e.Id,
                ItemOrTagId: e.ItemOrTag.Id,
                RequiredQuantity: Math.Abs(e.Quantity.GetDynamicValue(shoppingList) * parentRecipe.RoundFactor)))
            .ToList();

        if (demands.Count == 0)
        {
            return new IngredientCoverageSnapshot(new Dictionary<Guid, (decimal RequiredQuantity, decimal CoveredQuantity)>());
        }

        var relevantIngredientItemIds = demands
            .Select(d => d.ItemOrTagId)
            .ToHashSet();

        var supplies = BuildSuppliesFromUserRecipes(children, shoppingList, relevantIngredientItemIds);
        return ComputeCoverageSnapshot(demands, supplies, supplyMultiplier: 1m);
    }

    public static decimal GetCoveredQuantityForIngredient(UserRecipe parentRecipe, Element ingredient, DataContext shoppingList, IEnumerable<UserRecipe> children)
    {
        var snapshot = ComputeCoverage(parentRecipe, shoppingList, children);
        return snapshot.GetCoveredQuantity(ingredient);
    }

    public static int GetExpectedRoundFactor(UserRecipe parentRecipe, UserRecipe childRecipe, DataContext shoppingList)
    {
        var currentRoundFactor = Math.Max(childRecipe.RoundFactor, 1);

        var matchingIngredients = parentRecipe.Recipe.Elements
            .Where(e =>
                e.IsIngredient() &&
                childRecipe.Recipe.Elements.Any(product =>
                    product.IsProduct() &&
                    product.ItemOrTag.GetAssociatedTagsAndSelf().Any(iot => iot.Id == e.ItemOrTag.Id)))
            .OrderBy(e => e.Index)
            .ToList();

        if (matchingIngredients.Count == 0)
        {
            return currentRoundFactor;
        }

        var demands = matchingIngredients
            .Select(ingredient => new IngredientDemand(
                IngredientElementId: ingredient.Id,
                ItemOrTagId: ingredient.ItemOrTag.Id,
                RequiredQuantity: Math.Abs(ingredient.Quantity.GetDynamicValue(shoppingList) * parentRecipe.RoundFactor)))
            .ToList();

        var relevantIngredientItemIds = demands
            .Select(d => d.ItemOrTagId)
            .ToHashSet();

        var supplies = childRecipe.Recipe.Elements
            .Where(e => e.IsProduct())
            .Select(product =>
            {
                var supportedIngredientIds = product.ItemOrTag.GetAssociatedTagsAndSelf()
                    .Select(iot => iot.Id)
                    .Where(id => relevantIngredientItemIds.Contains(id))
                    .ToHashSet();

                var quantityPerCraft = product.Quantity.GetDynamicValue(shoppingList);
                return new ProductSupply(supportedIngredientIds, quantityPerCraft);
            })
            .Where(supply => supply.QuantityPerCraft > Epsilon && supply.SupportedIngredientItemOrTagIds.Count > 0)
            .ToList();

        if (supplies.Count == 0)
        {
            return currentRoundFactor;
        }

        var lowerBound = 1;
        foreach (var demand in demands)
        {
            var compatibleSupplyPerCraft = supplies
                .Where(s => s.SupportedIngredientItemOrTagIds.Contains(demand.ItemOrTagId))
                .Sum(s => s.QuantityPerCraft);

            if (compatibleSupplyPerCraft <= Epsilon)
            {
                return currentRoundFactor;
            }

            var bound = ClampDecimalCeilingToInt(demand.RequiredQuantity / compatibleSupplyPerCraft);
            lowerBound = Math.Max(lowerBound, bound);
        }

        if (AreAllDemandsFulfilled(demands, supplies, lowerBound))
        {
            return Math.Max(lowerBound, 1);
        }

        var upperBound = lowerBound;
        const int absoluteUpperBound = 1_000_000;
        while (upperBound < absoluteUpperBound && !AreAllDemandsFulfilled(demands, supplies, upperBound))
        {
            upperBound = Math.Min(upperBound * 2, absoluteUpperBound);
        }

        if (!AreAllDemandsFulfilled(demands, supplies, upperBound))
        {
            return currentRoundFactor;
        }

        var left = lowerBound;
        var right = upperBound;
        while (left < right)
        {
            var mid = left + (right - left) / 2;
            if (AreAllDemandsFulfilled(demands, supplies, mid))
            {
                right = mid;
            }
            else
            {
                left = mid + 1;
            }
        }

        return Math.Max(left, 1);
    }

    private static List<ProductSupply> BuildSuppliesFromUserRecipes(IEnumerable<UserRecipe> children, DataContext shoppingList, HashSet<Guid> relevantIngredientItemIds)
    {
        var supplies = new List<ProductSupply>();

        foreach (var childRecipe in children)
        {
            foreach (var product in childRecipe.Recipe.Elements.Where(e => e.IsProduct()).OrderBy(e => e.Index))
            {
                var supportedIngredientIds = product.ItemOrTag.GetAssociatedTagsAndSelf()
                    .Select(iot => iot.Id)
                    .Where(id => relevantIngredientItemIds.Contains(id))
                    .ToHashSet();

                if (supportedIngredientIds.Count == 0)
                {
                    continue;
                }

                var quantity = Math.Abs(product.Quantity.GetDynamicValue(shoppingList) * childRecipe.RoundFactor);
                if (quantity <= Epsilon)
                {
                    continue;
                }

                supplies.Add(new ProductSupply(supportedIngredientIds, quantity));
            }
        }

        return supplies;
    }

    private static IngredientCoverageSnapshot ComputeCoverageSnapshot(IReadOnlyList<IngredientDemand> demands, IReadOnlyList<ProductSupply> supplies, decimal supplyMultiplier)
    {
        var flowComputation = ComputeCoverageWithFlow(demands, supplies, supplyMultiplier);
        var coverageByIngredientId = new Dictionary<Guid, (decimal RequiredQuantity, decimal CoveredQuantity)>(demands.Count);

        for (var index = 0; index < demands.Count; index++)
        {
            var demand = demands[index];
            var coveredQuantity = flowComputation.CoveredQuantitiesByDemandIndex[index];
            coverageByIngredientId[demand.IngredientElementId] = (demand.RequiredQuantity, coveredQuantity);
        }

        return new IngredientCoverageSnapshot(coverageByIngredientId);
    }

    private static bool AreAllDemandsFulfilled(IReadOnlyList<IngredientDemand> demands, IReadOnlyList<ProductSupply> supplies, int roundFactor)
    {
        var flowComputation = ComputeCoverageWithFlow(demands, supplies, roundFactor);

        for (var index = 0; index < demands.Count; index++)
        {
            if (flowComputation.CoveredQuantitiesByDemandIndex[index] + Epsilon < demands[index].RequiredQuantity)
            {
                return false;
            }
        }

        return true;
    }

    private static FlowResult ComputeCoverageWithFlow(IReadOnlyList<IngredientDemand> demands, IReadOnlyList<ProductSupply> supplies, decimal supplyMultiplier)
    {
        var demandCount = demands.Count;
        var supplyCount = supplies.Count;

        if (demandCount == 0)
        {
            return new FlowResult(Array.Empty<decimal>());
        }

        if (supplyCount == 0 || supplyMultiplier <= Epsilon)
        {
            return new FlowResult(new decimal[demandCount]);
        }

        var sourceNode = 0;
        var firstSupplyNode = 1;
        var firstDemandNode = firstSupplyNode + supplyCount;
        var sinkNode = firstDemandNode + demandCount;

        var network = new FlowNetwork(sinkNode + 1);

        var totalDemand = demands.Sum(d => d.RequiredQuantity);
        var demandEdgeCapacity = Math.Max(totalDemand, 1m);

        for (var supplyIndex = 0; supplyIndex < supplyCount; supplyIndex++)
        {
            var supplyNode = firstSupplyNode + supplyIndex;
            var availableQuantity = supplies[supplyIndex].QuantityPerCraft * supplyMultiplier;
            if (availableQuantity > Epsilon)
            {
                network.AddEdge(sourceNode, supplyNode, availableQuantity);
            }
        }

        for (var demandIndex = 0; demandIndex < demandCount; demandIndex++)
        {
            var demandNode = firstDemandNode + demandIndex;
            var requiredQuantity = demands[demandIndex].RequiredQuantity;
            if (requiredQuantity > Epsilon)
            {
                network.AddEdge(demandNode, sinkNode, requiredQuantity);
            }
        }

        for (var supplyIndex = 0; supplyIndex < supplyCount; supplyIndex++)
        {
            var supplyNode = firstSupplyNode + supplyIndex;
            var supply = supplies[supplyIndex];

            for (var demandIndex = 0; demandIndex < demandCount; demandIndex++)
            {
                if (!supply.SupportedIngredientItemOrTagIds.Contains(demands[demandIndex].ItemOrTagId))
                {
                    continue;
                }

                var demandNode = firstDemandNode + demandIndex;
                network.AddEdge(supplyNode, demandNode, demandEdgeCapacity);
            }
        }

        network.MaxFlow(sourceNode, sinkNode);

        var coveredByDemandIndex = new decimal[demandCount];
        for (var demandIndex = 0; demandIndex < demandCount; demandIndex++)
        {
            var demandNode = firstDemandNode + demandIndex;
            var required = demands[demandIndex].RequiredQuantity;
            var remaining = network.GetResidualCapacity(demandNode, sinkNode);
            coveredByDemandIndex[demandIndex] = Math.Max(required - remaining, 0m);
        }

        return new FlowResult(coveredByDemandIndex);
    }

    private static int ClampDecimalCeilingToInt(decimal value)
    {
        if (value <= 1m)
        {
            return 1;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)decimal.Ceiling(value);
    }

    private sealed class FlowNetwork
    {
        private readonly List<int>[] _adjacency;
        private readonly decimal[,] _residualCapacities;

        public FlowNetwork(int nodeCount)
        {
            _adjacency = Enumerable.Range(0, nodeCount)
                .Select(_ => new List<int>())
                .ToArray();
            _residualCapacities = new decimal[nodeCount, nodeCount];
        }

        public void AddEdge(int from, int to, decimal capacity)
        {
            if (capacity <= Epsilon)
            {
                return;
            }

            if (_residualCapacities[from, to] <= Epsilon && _residualCapacities[to, from] <= Epsilon)
            {
                _adjacency[from].Add(to);
                _adjacency[to].Add(from);
            }

            _residualCapacities[from, to] += capacity;
        }

        public decimal MaxFlow(int source, int sink)
        {
            var maxFlow = 0m;

            while (true)
            {
                var visited = new bool[_adjacency.Length];
                var flow = DepthFirstAugment(source, sink, decimal.MaxValue, visited);
                if (flow <= Epsilon)
                {
                    break;
                }

                maxFlow += flow;
            }

            return maxFlow;
        }

        public decimal GetResidualCapacity(int from, int to)
        {
            return _residualCapacities[from, to];
        }

        private decimal DepthFirstAugment(int current, int sink, decimal incomingFlow, bool[] visited)
        {
            if (current == sink)
            {
                return incomingFlow;
            }

            visited[current] = true;

            foreach (var next in _adjacency[current])
            {
                var residual = _residualCapacities[current, next];
                if (visited[next] || residual <= Epsilon)
                {
                    continue;
                }

                var possibleFlow = DepthFirstAugment(next, sink, Math.Min(incomingFlow, residual), visited);
                if (possibleFlow <= Epsilon)
                {
                    continue;
                }

                _residualCapacities[current, next] -= possibleFlow;
                _residualCapacities[next, current] += possibleFlow;
                return possibleFlow;
            }

            return 0m;
        }
    }

    private sealed record FlowResult(decimal[] CoveredQuantitiesByDemandIndex);
}
