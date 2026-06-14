using System.Globalization;
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

    // Liste des produits finaux d'une shopping list (sorties des recettes racines) avec, comme débit
    // cible par défaut, le débit d'UNE table de la recette finale (quantité produite / temps de craft).
    // Sert à alimenter le panneau de saisie du mode automatisation.
    public List<AutomationTarget> GetAutomationTargets(DataContext shoppingList)
    {
        var targets = new Dictionary<Guid, AutomationTarget>();

        foreach (var rootRecipe in shoppingList.GetRootShoppingListRecipes())
        {
            foreach (var product in rootRecipe.Recipe.Elements
                         .Where(e => e.IsProduct() && !e.DefaultIsReintegrated)
                         .OrderBy(e => e.Index))
            {
                var producedPerCraft = product.Quantity.GetDynamicValue(shoppingList);
                if (producedPerCraft <= Epsilon)
                {
                    continue;
                }

                var defaultRate = PerMinuteRate(producedPerCraft, rootRecipe.Recipe, shoppingList);
                var id = product.ItemOrTag.Id;
                if (targets.TryGetValue(id, out var existing))
                {
                    existing.DefaultRate += defaultRate;
                }
                else
                {
                    targets[id] = new AutomationTarget
                    {
                        ItemId = id,
                        Name = localizationService.GetTranslation(product.ItemOrTag),
                        // Pleine précision interne (l'affichage reste à 2 décimales via Format="0.##").
                        // Garder 1/3 exact évite à la fois ×0,99 au lieu de ×1 et, en /h, 19,98 au lieu de 20.
                        DefaultRate = defaultRate,
                    };
                }
            }
        }

        return targets.Values.OrderBy(t => t.Name).ToList();
    }

    // Matières premières distinctes d'une shopping list : items des ingrédients non couverts par une
    // recette de l'arbre (donc « à acheter »). Topologie seule, indépendant des débits → stable, sert
    // à alimenter le panneau de limites d'entrée du mode automatisation.
    public List<AutomationInput> GetAutomationInputs(DataContext shoppingList)
    {
        var inputs = new Dictionary<Guid, AutomationInput>();

        foreach (var parentRecipe in shoppingList.UserRecipes)
        {
            var coverage = ShoppingListCoverageCalculator.ComputeCoverage(parentRecipe, shoppingList, parentRecipe.ChildrenUserRecipes);

            foreach (var ingredient in parentRecipe.Recipe.Elements.Where(e => e.IsIngredient()).OrderBy(e => e.Index))
            {
                if (coverage.GetMissingQuantity(ingredient) <= Epsilon)
                {
                    continue;
                }

                var id = ingredient.ItemOrTag.Id;
                inputs.TryAdd(id, new AutomationInput
                {
                    ItemId = id,
                    Name = localizationService.GetTranslation(ingredient.ItemOrTag),
                });
            }
        }

        return inputs.Values.OrderBy(i => i.Name).ToList();
    }

    // Planificateur d'usine en régime permanent : 1 nœud = 1 table de craft à débit fixe
    // (débit = quantité produite par craft / temps de craft). À partir d'un débit /min cible par
    // produit final, on propage la demande de l'aval vers l'amont sur l'arbre de recettes déjà choisi
    // et on en déduit le nombre (fractionnaire) de tables par recette et le débit transitant sur
    // chaque arête. La TOPOLOGIE (ids des nœuds/arêtes, ordre) est identique à BuildGraphData : seuls
    // les libellés changent, ce qui permet une mise à jour côté client sans re-layout.
    public ProductionGraphData BuildAutomationGraphData(DataContext shoppingList, IReadOnlyDictionary<Guid, decimal> targetRates, IReadOnlyDictionary<Guid, decimal> inputCaps)
    {
        var data = new ProductionGraphData
        {
            FallbackImage = IconUrl("EmptyIcon"),
        };

        // 1. Recettes (un nœud par recette) + quantités servant de POIDS de répartition de la demande
        //    quand plusieurs producteurs (ou un achat) couvrent un même ingrédient.
        var recipesById = new Dictionary<Guid, Recipe>();
        foreach (var group in shoppingList.UserRecipes.GroupBy(ur => ur.RecipeId))
        {
            recipesById[group.Key] = group.First().Recipe;
        }

        var edgeQty = new Dictionary<(Guid Producer, Guid Consumer, Guid Item), decimal>();
        var edgeChannel = new Dictionary<(Guid Producer, Guid Consumer, Guid Item), ItemOrTag>();
        var purchaseQty = new Dictionary<(Guid Consumer, Guid Item), decimal>();
        var purchaseChannel = new Dictionary<(Guid Consumer, Guid Item), ItemOrTag>();

        // Graphe consommateur -> producteur pour le tri topologique (consommateurs traités d'abord).
        var producersOf = new Dictionary<Guid, HashSet<Guid>>();
        var inDegree = recipesById.Keys.ToDictionary(id => id, _ => 0);

        foreach (var parentRecipe in shoppingList.UserRecipes)
        {
            var coverage = ShoppingListCoverageCalculator.ComputeCoverage(parentRecipe, shoppingList, parentRecipe.ChildrenUserRecipes);

            foreach (var ingredient in parentRecipe.Recipe.Elements.Where(e => e.IsIngredient()).OrderBy(e => e.Index))
            {
                foreach (var child in GetMatchingChildren(parentRecipe, ingredient))
                {
                    if (child.RecipeId == parentRecipe.RecipeId)
                    {
                        continue;
                    }

                    var producedPerCraft = child.Recipe.Elements
                        .Where(p => p.IsProduct()
                            && !p.DefaultIsReintegrated
                            && ShoppingListCoverageCalculator.CanSupplyIngredient(p.ItemOrTag, ingredient.ItemOrTag))
                        .Sum(p => p.Quantity.GetDynamicValue(shoppingList));

                    var key = (child.RecipeId, parentRecipe.RecipeId, ingredient.ItemOrTag.Id);
                    edgeQty[key] = edgeQty.GetValueOrDefault(key) + producedPerCraft * child.RoundFactor;
                    edgeChannel[key] = ingredient.ItemOrTag;

                    producersOf.TryAdd(parentRecipe.RecipeId, []);
                    if (producersOf[parentRecipe.RecipeId].Add(child.RecipeId))
                    {
                        inDegree[child.RecipeId] += 1;
                    }
                }

                var missing = coverage.GetMissingQuantity(ingredient);
                if (missing > Epsilon)
                {
                    var pk = (parentRecipe.RecipeId, ingredient.ItemOrTag.Id);
                    purchaseQty[pk] = purchaseQty.GetValueOrDefault(pk) + missing;
                    purchaseChannel[pk] = ingredient.ItemOrTag;
                }
            }
        }

        // 2. Propagation du débit demandé (aval -> amont) sur le DAG des recettes.
        var demandRate = new Dictionary<(Guid Recipe, Guid Item), decimal>();
        var demandChannel = new Dictionary<(Guid Recipe, Guid Item), ItemOrTag>();
        var tablesByRecipe = new Dictionary<Guid, decimal>();
        var edgeRate = new Dictionary<(Guid Producer, Guid Consumer, Guid Item), decimal>();
        var purchaseRate = new Dictionary<(Guid Consumer, Guid Item), decimal>();

        void AddDemand(Guid recipeId, ItemOrTag channel, decimal rate)
        {
            var key = (recipeId, channel.Id);
            demandRate[key] = demandRate.GetValueOrDefault(key) + rate;
            demandChannel[key] = channel;
        }

        // Amorçage : chaque produit final reçoit le débit cible saisi (ou le débit d'une table par défaut).
        foreach (var rootRecipe in shoppingList.GetRootShoppingListRecipes())
        {
            foreach (var product in rootRecipe.Recipe.Elements.Where(e => e.IsProduct() && !e.DefaultIsReintegrated).OrderBy(e => e.Index))
            {
                var producedPerCraft = product.Quantity.GetDynamicValue(shoppingList);
                if (producedPerCraft <= Epsilon)
                {
                    continue;
                }

                var rate = targetRates.TryGetValue(product.ItemOrTag.Id, out var target)
                    ? target
                    : PerMinuteRate(producedPerCraft, rootRecipe.Recipe, shoppingList);
                AddDemand(rootRecipe.RecipeId, product.ItemOrTag, rate);
            }
        }

        var queue = new Queue<Guid>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var processed = new HashSet<Guid>();

        void Process(Guid recipeId)
        {
            if (!processed.Add(recipeId))
            {
                return;
            }

            var recipe = recipesById[recipeId];

            // tables(R) = max sur les sorties demandées de ( demande / débit d'une table ).
            decimal tables = 0m;
            foreach (var (key, dr) in demandRate.Where(kv => kv.Key.Recipe == recipeId))
            {
                var perTableOut = PerTableOutForChannel(recipe, demandChannel[key], shoppingList);
                if (perTableOut > Epsilon)
                {
                    tables = Math.Max(tables, dr / perTableOut);
                }
            }
            tablesByRecipe[recipeId] = tables;

            // Répartition de la demande de chaque ingrédient vers ses producteurs (et l'achat).
            foreach (var ingredient in recipe.Elements.Where(e => e.IsIngredient()).OrderBy(e => e.Index))
            {
                var needRate = tables * PerMinuteRate(Math.Abs(ingredient.Quantity.GetDynamicValue(shoppingList)), recipe, shoppingList);
                if (needRate <= Epsilon)
                {
                    continue;
                }

                var channelId = ingredient.ItemOrTag.Id;
                var producerKeys = edgeQty.Keys.Where(k => k.Consumer == recipeId && k.Item == channelId).ToList();
                var totalQty = producerKeys.Sum(k => edgeQty[k]) + purchaseQty.GetValueOrDefault((recipeId, channelId));
                if (totalQty <= Epsilon)
                {
                    continue;
                }

                foreach (var key in producerKeys)
                {
                    var rate = needRate * edgeQty[key] / totalQty;
                    edgeRate[key] = edgeRate.GetValueOrDefault(key) + rate;
                    AddDemand(key.Producer, ingredient.ItemOrTag, rate);
                }

                var pk = (recipeId, channelId);
                if (purchaseQty.TryGetValue(pk, out var pq))
                {
                    purchaseRate[pk] = purchaseRate.GetValueOrDefault(pk) + needRate * pq / totalQty;
                }
            }

            if (producersOf.TryGetValue(recipeId, out var children))
            {
                foreach (var child in children)
                {
                    inDegree[child] -= 1;
                    if (inDegree[child] == 0)
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }

        while (queue.Count > 0)
        {
            Process(queue.Dequeue());
        }

        // Filet de sécurité si l'arbre contient un cycle (théoriquement impossible) : on traite le reste.
        foreach (var recipeId in recipesById.Keys)
        {
            Process(recipeId);
        }

        // 2bis. Goulots : si une matière première plafonnée ne couvre pas le débit demandé, on bride
        //       TOUTE la chaîne (tous les débits/tables sont linéaires dans les cibles) d'un facteur
        //       global = min des (cap / demande) des entrées, jamais > 1. Les items dont le facteur
        //       est le plus contraignant sont les goulots.
        var demandedByInput = new Dictionary<Guid, decimal>();
        foreach (var ((_, itemId), rate) in purchaseRate)
        {
            demandedByInput[itemId] = demandedByInput.GetValueOrDefault(itemId) + rate;
        }

        var globalFactor = 1m;
        foreach (var (itemId, demanded) in demandedByInput)
        {
            if (inputCaps.TryGetValue(itemId, out var cap) && demanded > Epsilon)
            {
                globalFactor = Math.Min(globalFactor, cap / demanded);
            }
        }

        var bottleneckItems = new HashSet<Guid>();
        if (globalFactor < 1m)
        {
            foreach (var (itemId, demanded) in demandedByInput)
            {
                if (inputCaps.TryGetValue(itemId, out var cap) && demanded > Epsilon
                    && Math.Abs(cap / demanded - globalFactor) <= Epsilon)
                {
                    bottleneckItems.Add(itemId);
                }
            }
        }

        // 3. Matérialisation des nœuds et arêtes (mêmes ids/ordre que BuildGraphData).
        var craftingNodeIds = new Dictionary<Guid, string>();
        foreach (var group in shoppingList.UserRecipes.GroupBy(ur => ur.RecipeId))
        {
            var recipe = group.First().Recipe;
            var nodeId = "r:" + group.Key;
            var tables = tablesByRecipe.GetValueOrDefault(group.Key) * globalFactor;

            data.Nodes.Add(new ProductionGraphNode
            {
                Id = nodeId,
                Type = "crafting",
                Image = IconUrl(recipe.CraftingTable.Name),
                Label = $"×{FormatTables(tables)} {localizationService.GetTranslation(recipe.CraftingTable)}\n({localizationService.GetTranslation(recipe)})",
            });

            craftingNodeIds[group.Key] = nodeId;
        }

        foreach (var (key, channel) in edgeChannel)
        {
            data.Edges.Add(BuildRateEdge(craftingNodeIds[key.Producer], craftingNodeIds[key.Consumer], channel, edgeRate.GetValueOrDefault(key) * globalFactor));
        }

        var leafIndex = 0;
        foreach (var (key, channel) in purchaseChannel)
        {
            var leafId = "b:" + leafIndex++;
            data.Nodes.Add(new ProductionGraphNode
            {
                Id = leafId,
                Type = "buy",
                Image = IconUrl(channel.Name),
                Label = localizationService.GetTranslation(channel),
                Bottleneck = bottleneckItems.Contains(channel.Id),
            });
            data.Edges.Add(BuildRateEdge(leafId, craftingNodeIds[key.Consumer], channel, purchaseRate.GetValueOrDefault(key) * globalFactor));
        }

        foreach (var rootRecipe in shoppingList.GetRootShoppingListRecipes())
        {
            foreach (var product in rootRecipe.Recipe.Elements.Where(e => e.IsProduct() && !e.DefaultIsReintegrated).OrderBy(e => e.Index))
            {
                var producedPerCraft = product.Quantity.GetDynamicValue(shoppingList);
                if (producedPerCraft <= Epsilon)
                {
                    continue;
                }

                var rate = tablesByRecipe.GetValueOrDefault(rootRecipe.RecipeId) * PerTableOutForChannel(rootRecipe.Recipe, product.ItemOrTag, shoppingList) * globalFactor;
                var leafId = "f:" + leafIndex++;
                data.Nodes.Add(new ProductionGraphNode
                {
                    Id = leafId,
                    Type = "final",
                    Image = IconUrl(product.ItemOrTag.Name),
                    Label = localizationService.GetTranslation(product.ItemOrTag),
                });
                data.Edges.Add(BuildRateEdge(craftingNodeIds[rootRecipe.RecipeId], leafId, product.ItemOrTag, rate));
            }
        }

        return data;
    }

    // Débit d'UNE table de craft pour le canal (ingrédient/tag) donné : somme des produits de la
    // recette capables de fournir ce canal, divisée par le temps de craft.
    private decimal PerTableOutForChannel(Recipe recipe, ItemOrTag channel, DataContext shoppingList)
    {
        var producedPerCraft = recipe.Elements
            .Where(p => p.IsProduct()
                && !p.DefaultIsReintegrated
                && ShoppingListCoverageCalculator.CanSupplyIngredient(p.ItemOrTag, channel))
            .Sum(p => p.Quantity.GetDynamicValue(shoppingList));

        return PerMinuteRate(producedPerCraft, recipe, shoppingList);
    }

    private ProductionGraphEdge BuildRateEdge(string from, string to, ItemOrTag channel, decimal rate)
    {
        return new ProductionGraphEdge
        {
            From = from,
            To = to,
            Item = localizationService.GetTranslation(channel),
            Quantity = 0m,
            // Pleine précision : l'arrondi d'affichage (2 décimales) est fait côté JS APRÈS la
            // conversion éventuelle en /h (×60). Pré-arrondir ici donnerait 0,67×60 = 40,2 au lieu de 40.
            PerMinute = rate,
        };
    }

    private static string FormatTables(decimal tables)
    {
        return Math.Round(tables, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
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
    public bool Bottleneck { get; set; }
}

public class ProductionGraphEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Item { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal PerMinute { get; set; }
}

public class AutomationTarget
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = "";
    public decimal DefaultRate { get; set; }
}

public class AutomationInput
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = "";
}
