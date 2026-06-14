using ecocraft.Models;

namespace ecocraft.Services;

// Construit les données de la chaîne de production d'une shopping list (nœuds + arêtes) destinées
// au rendu vis-network, façon planner Satisfactory : un nœud par RECETTE (toutes ses occurrences
// de l'arbre fusionnées, crafts cumulés), si bien qu'un producteur partagé apparaît une seule fois
// avec plusieurs sorties. Une même table de craft peut apparaître dans plusieurs nœuds si elle
// porte des recettes différentes. Les matières à acheter sont des sources, les produits finaux des
// puits, et chaque arête porte la quantité circulant.
public class ShoppingListGraphService(LocalizationService localizationService, ContextService contextService)
{
    private const decimal Epsilon = 0.000001m;

    public ProductionGraphData BuildGraphData(DataContext shoppingList)
    {
        var data = new ProductionGraphData
        {
            FallbackImage = IconUrl("EmptyIcon"),
        };

        // 1. Un nœud par recette, avec le total de crafts (somme des RoundFactor de ses occurrences).
        var craftingNodeIds = new Dictionary<Guid, string>();
        foreach (var group in shoppingList.UserRecipes.GroupBy(ur => ur.RecipeId))
        {
            var recipe = group.First().Recipe;
            var totalCrafts = group.Sum(ur => ur.RoundFactor);
            var nodeId = "r:" + group.Key;

            data.Nodes.Add(new ProductionGraphNode
            {
                Id = nodeId,
                Type = "crafting",
                Image = IconUrl(recipe.CraftingTable.Name),
                Label = $"×{totalCrafts} {localizationService.GetTranslation(recipe.CraftingTable)}\n({localizationService.GetTranslation(recipe)})",
            });

            craftingNodeIds[group.Key] = nodeId;
        }

        // 2. Agrégation des flux sur l'ensemble des occurrences de l'arbre.
        var recipeFlows = new Dictionary<(Guid ProducerRecipeId, Guid ConsumerRecipeId, Guid ItemId), Flow>();
        var purchases = new Dictionary<(Guid ConsumerRecipeId, Guid ItemId), Flow>();
        var finalProducts = new Dictionary<(Guid RootRecipeId, Guid ItemId), Flow>();

        foreach (var parentRecipe in shoppingList.UserRecipes)
        {
            var coverage = ShoppingListCoverageCalculator.ComputeCoverage(parentRecipe, shoppingList, parentRecipe.ChildrenUserRecipes);

            foreach (var ingredient in parentRecipe.Recipe.Elements.Where(e => e.IsIngredient()).OrderBy(e => e.Index))
            {
                foreach (var child in GetMatchingChildren(parentRecipe, ingredient))
                {
                    if (child.RecipeId == parentRecipe.RecipeId)
                    {
                        continue; // évite une boucle sur le même nœud après fusion
                    }

                    var producedPerCraft = child.Recipe.Elements
                        .Where(p => p.IsProduct()
                            && !p.DefaultIsReintegrated
                            && ShoppingListCoverageCalculator.CanSupplyIngredient(p.ItemOrTag, ingredient.ItemOrTag))
                        .Sum(p => p.Quantity.GetDynamicValue(shoppingList));

                    var producedQuantity = producedPerCraft * child.RoundFactor;
                    var producedPerMinute = PerMinuteRate(producedPerCraft, child.Recipe, shoppingList);

                    Accumulate(recipeFlows, (child.RecipeId, parentRecipe.RecipeId, ingredient.ItemOrTag.Id), ingredient.ItemOrTag, producedQuantity, producedPerMinute);
                }

                var missing = coverage.GetMissingQuantity(ingredient);
                if (missing > Epsilon)
                {
                    var consumedPerCraft = Math.Abs(ingredient.Quantity.GetDynamicValue(shoppingList));
                    var consumedPerMinute = PerMinuteRate(consumedPerCraft, parentRecipe.Recipe, shoppingList);
                    Accumulate(purchases, (parentRecipe.RecipeId, ingredient.ItemOrTag.Id), ingredient.ItemOrTag, missing, consumedPerMinute);
                }
            }
        }

        foreach (var rootRecipe in shoppingList.GetRootShoppingListRecipes())
        {
            foreach (var product in rootRecipe.Recipe.Elements.Where(e => e.IsProduct() && !e.DefaultIsReintegrated).OrderBy(e => e.Index))
            {
                var producedPerCraft = product.Quantity.GetDynamicValue(shoppingList);
                var quantity = producedPerCraft * rootRecipe.RoundFactor;
                if (quantity > Epsilon)
                {
                    var perMinute = PerMinuteRate(producedPerCraft, rootRecipe.Recipe, shoppingList);
                    Accumulate(finalProducts, (rootRecipe.RecipeId, product.ItemOrTag.Id), product.ItemOrTag, quantity, perMinute);
                }
            }
        }

        // 3. Matérialisation des arêtes (+ feuilles d'achat / produits finaux dédiées par flux).
        foreach (var (key, flow) in recipeFlows)
        {
            data.Edges.Add(BuildEdge(craftingNodeIds[key.ProducerRecipeId], craftingNodeIds[key.ConsumerRecipeId], flow));
        }

        var leafIndex = 0;
        foreach (var (key, flow) in purchases)
        {
            var leafId = "b:" + leafIndex++;
            data.Nodes.Add(new ProductionGraphNode
            {
                Id = leafId,
                Type = "buy",
                Image = IconUrl(flow.Item.Name),
                Label = localizationService.GetTranslation(flow.Item),
            });
            data.Edges.Add(BuildEdge(leafId, craftingNodeIds[key.ConsumerRecipeId], flow));
        }

        foreach (var (key, flow) in finalProducts)
        {
            var leafId = "f:" + leafIndex++;
            data.Nodes.Add(new ProductionGraphNode
            {
                Id = leafId,
                Type = "final",
                Image = IconUrl(flow.Item.Name),
                Label = localizationService.GetTranslation(flow.Item),
            });
            data.Edges.Add(BuildEdge(craftingNodeIds[key.RootRecipeId], leafId, flow));
        }

        return data;
    }

    private static decimal PerMinuteRate(decimal quantityPerCraft, Recipe recipe, DataContext shoppingList)
    {
        // CraftMinutes peut ne pas être chargé selon le contexte : on dégrade en 0 plutôt que de planter.
        if (recipe.CraftMinutes is null)
        {
            return 0m;
        }

        var craftMinutes = recipe.CraftMinutes.GetDynamicValue(shoppingList);
        return craftMinutes > Epsilon ? quantityPerCraft / craftMinutes : 0m;
    }

    private ProductionGraphEdge BuildEdge(string from, string to, Flow flow)
    {
        return new ProductionGraphEdge
        {
            From = from,
            To = to,
            Item = localizationService.GetTranslation(flow.Item),
            Quantity = Math.Round(flow.Quantity, 2, MidpointRounding.AwayFromZero),
            PerMinute = Math.Round(flow.PerMinute, 2, MidpointRounding.AwayFromZero),
        };
    }

    private string IconUrl(string iconName)
    {
        var serverId = contextService.CurrentServer?.Id;
        return $"/assets/eco-icons/{iconName}.png?serverId={serverId}";
    }

    private sealed class Flow
    {
        public decimal Quantity;
        public decimal PerMinute;
        public ItemOrTag Item = null!;
    }

    private static void Accumulate<TKey>(Dictionary<TKey, Flow> map, TKey key, ItemOrTag item, decimal quantity, decimal perMinute) where TKey : notnull
    {
        if (!map.TryGetValue(key, out var flow))
        {
            // Le débit/min est un débit par table de craft (constant pour un couple recette/item),
            // donc fixé à la création ; seule la quantité totale s'accumule sur les occurrences.
            flow = new Flow { Item = item, PerMinute = perMinute };
            map[key] = flow;
        }

        flow.Quantity += quantity;
    }

    private static IEnumerable<UserRecipe> GetMatchingChildren(UserRecipe parentRecipe, Element ingredient)
    {
        return parentRecipe.ChildrenUserRecipes
            .Where(child => child.Recipe.Elements.Any(product =>
                product.IsProduct()
                && !product.DefaultIsReintegrated
                && ShoppingListCoverageCalculator.CanSupplyIngredient(product.ItemOrTag, ingredient.ItemOrTag)));
    }

}

public class ProductionGraphData
{
    public List<ProductionGraphNode> Nodes { get; set; } = [];
    public List<ProductionGraphEdge> Edges { get; set; } = [];
    public string FallbackImage { get; set; } = "";
}

public class ProductionGraphNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Image { get; set; } = "";
    public string Label { get; set; } = "";
}

public class ProductionGraphEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Item { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal PerMinute { get; set; }
}
