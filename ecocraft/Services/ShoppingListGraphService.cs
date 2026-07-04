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
                        ItemOrTag = product.ItemOrTag,
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
                    ItemOrTag = ingredient.ItemOrTag,
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
    //
    // maxItems : produits finaux dont le débit cible n'est pas saisi mais « maximisé » sous les
    // contraintes d'entrée. Le modèle étant linéaire, on résout le débit max par superposition (voir
    // ResolveTargetRates) avant la propagation finale.
    public AutomationGraphResult BuildAutomationGraphData(
        DataContext shoppingList,
        IReadOnlyDictionary<Guid, decimal> fixedRates,
        IReadOnlyDictionary<Guid, decimal> inputCaps,
        IReadOnlySet<Guid> maxItems)
    {
        var topology = ComputeTopology(shoppingList);

        // Trois familles de cibles, servies dans l'ordre FIXES → MAX → NEUTRES (cf. ResolveAutomation) :
        // FIXES (débit saisi, prioritaires), « MAX » (maximisées sur la capacité restante après les fixes),
        // NEUTRES (champ vide → débit par défaut de la recette, sur ce qu'il reste). Les neutres et les bases
        // « max » se déduisent de la liste des produits finaux.
        var targets = GetAutomationTargets(shoppingList);
        var fixedSeed = targets
            .Where(t => fixedRates.ContainsKey(t.ItemId) && !maxItems.Contains(t.ItemId))
            .ToDictionary(t => t.ItemId, t => fixedRates[t.ItemId]);
        var neutralSeed = targets
            .Where(t => !fixedRates.ContainsKey(t.ItemId) && !maxItems.Contains(t.ItemId))
            .ToDictionary(t => t.ItemId, t => t.DefaultRate);
        var maxSeed = targets
            .Where(t => maxItems.Contains(t.ItemId))
            .ToDictionary(t => t.ItemId, t => t.DefaultRate);

        var resolution = ResolveAutomation(topology, shoppingList, fixedSeed, neutralSeed, maxSeed, inputCaps);
        var propagation = Propagate(topology, shoppingList, resolution.SeedRates);
        var bottleneckItems = resolution.BottleneckInputs;
        var surplusPlan = ComputeSurplusPlan(topology, shoppingList, propagation, inputCaps);

        // 3. Matérialisation des nœuds et arêtes (mêmes ids/ordre que BuildGraphData). La mise à l'échelle
        // par priorité est déjà intégrée aux débits résolus : il n'y a plus de facteur global à appliquer.
        var data = new ProductionGraphData
        {
            FallbackImage = IconUrl("EmptyIcon"),
        };

        var craftingNodeIds = new Dictionary<Guid, string>();
        foreach (var group in shoppingList.UserRecipes.GroupBy(ur => ur.RecipeId))
        {
            var recipe = group.First().Recipe;
            var nodeId = "r:" + group.Key;
            var tables = surplusPlan.Tables.TryGetValue(group.Key, out var maxedTables)
                ? maxedTables
                : propagation.TablesByRecipe.GetValueOrDefault(group.Key);

            data.Nodes.Add(new ProductionGraphNode
            {
                Id = nodeId,
                Type = "crafting",
                Image = IconUrl(recipe.CraftingTable.Name),
                Label = $"×{FormatTables(tables)} {localizationService.GetTranslation(recipe.CraftingTable)}\n({localizationService.GetTranslation(recipe)})",
            });

            craftingNodeIds[group.Key] = nodeId;
        }

        foreach (var (key, channel) in topology.EdgeChannel)
        {
            data.Edges.Add(BuildRateEdge(craftingNodeIds[key.Producer], craftingNodeIds[key.Consumer], channel, propagation.EdgeRate.GetValueOrDefault(key)));
        }

        // Noms des items goulots (limites saturées ayant raboté un palier fixe/neutre), pour le tooltip
        // orange des cibles non atteintes. Dédupliqués sur l'item (un achat peut alimenter plusieurs nœuds).
        var bottleneckNames = new List<string>();
        var bottleneckSeen = new HashSet<Guid>();
        var leafIndex = 0;
        foreach (var (key, channel) in topology.PurchaseChannel)
        {
            var leafId = "b:" + leafIndex++;
            var isBottleneck = bottleneckItems.Contains(channel.Id);
            if (isBottleneck && bottleneckSeen.Add(channel.Id))
            {
                bottleneckNames.Add(localizationService.GetTranslation(channel));
            }

            data.Nodes.Add(new ProductionGraphNode
            {
                Id = leafId,
                Type = "buy",
                Image = IconUrl(channel.Name),
                Label = localizationService.GetTranslation(channel),
                Bottleneck = isBottleneck,
            });
            // Achat éventuellement majoré quand l'atelier source est poussé à saturer sa limite (cf. surplus).
            var purchaseRate = surplusPlan.Purchase.TryGetValue(key, out var maxedPurchase)
                ? maxedPurchase
                : propagation.PurchaseRate.GetValueOrDefault(key);
            data.Edges.Add(BuildRateEdge(leafId, craftingNodeIds[key.Consumer], channel, purchaseRate));
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

                var rate = propagation.TablesByRecipe.GetValueOrDefault(rootRecipe.RecipeId) * PerTableOutForChannel(rootRecipe.Recipe, product.ItemOrTag, shoppingList);
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

        // Surplus : ateliers source poussés à saturer leur limite d'entrée et qui produisent plus que ce que
        // l'aval consomme → l'excédent part vers un nœud ressource dédié (cf. ComputeSurplusPlan).
        foreach (var (recipeId, item, rate) in surplusPlan.Surplus)
        {
            var leafId = "s:" + leafIndex++;
            data.Nodes.Add(new ProductionGraphNode
            {
                Id = leafId,
                Type = "surplus",
                Image = IconUrl(item.Name),
                Label = localizationService.GetTranslation(item),
            });
            data.Edges.Add(BuildRateEdge(craftingNodeIds[recipeId], leafId, item, rate));
        }

        return new AutomationGraphResult
        {
            Data = data,
            // SeedRates = débit RÉELLEMENT produit par cible (après mise à l'échelle des paliers) : sert à
            // afficher les « max » résolus ET à détecter les objectifs fixes rabotés (réel < saisi).
            ResolvedTargetRates = resolution.SeedRates,
            UnboundedMaxItems = resolution.UnboundedMax,
            BottleneckItemNames = bottleneckNames,
        };
    }

    // Résout les débits effectifs par PRIORITÉ décroissante : objectifs FIXES d'abord (servis tels quels,
    // réduits proportionnellement seulement s'ils saturent à eux seuls une limite d'entrée), puis cibles
    // « MAX » poussées au plus haut que la capacité restante autorise, enfin cibles NEUTRES au débit par
    // défaut (ajustées sur ce qu'il reste). Une cible explicitement maximisée est donc prioritaire sur une
    // cible neutre (champ vide = simple défaut de recette) qui partage le même intrant : sans cette priorité,
    // la neutre consommait toute la capacité partagée et la « max » tombait à 0 — voire EN DESSOUS du débit
    // qu'elle aurait eu sans « max » (cas du partage du cuivre Fil/Plaque). Le modèle étant linéaire, chaque
    // palier = une propagation mise à l'échelle par un scalaire saturant la limite la plus contraignante du
    // palier. Une limite qui rabote un palier fixe/neutre (échelle < 1) est un goulot. Une « max » sans limite
    // contraignante reste illimitée.
    private ResolvedAutomation ResolveAutomation(
        AutomationTopology topology,
        DataContext shoppingList,
        IReadOnlyDictionary<Guid, decimal> fixedSeed,
        IReadOnlyDictionary<Guid, decimal> neutralSeed,
        IReadOnlyDictionary<Guid, decimal> maxSeed,
        IReadOnlyDictionary<Guid, decimal> inputCaps)
    {
        var result = new ResolvedAutomation();
        var remaining = new Dictionary<Guid, decimal>(inputCaps);

        // Palier de DEMANDE (fixe ou neutre) : servi au débit voulu, réduit (échelle ≤ 1) si une limite déjà
        // entamée par les paliers précédents ne suffit pas. Les limites forçant la réduction sont des goulots.
        void ApplyDemandTier(IReadOnlyDictionary<Guid, decimal> seeds)
        {
            if (seeds.Count == 0)
            {
                return;
            }

            var demand = DemandByInput(Propagate(topology, shoppingList, seeds));

            var scale = 1m;
            foreach (var (itemId, used) in demand)
            {
                if (used > Epsilon && remaining.TryGetValue(itemId, out var available))
                {
                    scale = Math.Min(scale, Math.Max(0m, available / used));
                }
            }

            foreach (var (itemId, rate) in seeds)
            {
                result.SeedRates[itemId] = rate * scale;
            }

            if (scale < 1m - Epsilon)
            {
                foreach (var (itemId, used) in demand)
                {
                    if (used > Epsilon && remaining.TryGetValue(itemId, out var available)
                        && Math.Abs(Math.Max(0m, available / used) - scale) <= Epsilon)
                    {
                        result.BottleneckInputs.Add(itemId);
                    }
                }
            }

            foreach (var (itemId, used) in demand)
            {
                if (remaining.ContainsKey(itemId))
                {
                    remaining[itemId] = Math.Max(0m, remaining[itemId] - used * scale);
                }
            }
        }

        ApplyDemandTier(fixedSeed);

        // Palier « MAX » AVANT le palier neutre : facteur t le plus grand applicable aux bases sans dépasser
        // une limite restante. Aucune limite contraignante → production illimitée (base conservée, signalée).
        if (maxSeed.Count > 0)
        {
            var demand = DemandByInput(Propagate(topology, shoppingList, maxSeed));

            var t = decimal.MaxValue;
            foreach (var (itemId, perBasis) in demand)
            {
                if (perBasis > Epsilon && remaining.TryGetValue(itemId, out var available))
                {
                    t = Math.Min(t, Math.Max(0m, available / perBasis));
                }
            }

            if (t == decimal.MaxValue)
            {
                foreach (var (itemId, rate) in maxSeed)
                {
                    result.SeedRates[itemId] = rate; // base conservée pour un graphe lisible
                    result.UnboundedMax.Add(itemId);
                }
            }
            else
            {
                foreach (var (itemId, rate) in maxSeed)
                {
                    result.SeedRates[itemId] = rate * t;
                }
                foreach (var (itemId, perBasis) in demand)
                {
                    if (remaining.ContainsKey(itemId))
                    {
                        remaining[itemId] = Math.Max(0m, remaining[itemId] - perBasis * t);
                    }
                }
            }
        }

        // Palier NEUTRE en dernier : récupère la capacité laissée par les paliers fixe et « max ».
        ApplyDemandTier(neutralSeed);

        return result;
    }

    // 1. Partie indépendante des débits : recettes (un nœud par recette) + quantités servant de POIDS de
    //    répartition de la demande quand plusieurs producteurs (ou un achat) couvrent un même ingrédient,
    //    et le graphe consommateur -> producteur (degrés entrants) pour le tri topologique.
    private AutomationTopology ComputeTopology(DataContext shoppingList)
    {
        var topology = new AutomationTopology();

        foreach (var group in shoppingList.UserRecipes.GroupBy(ur => ur.RecipeId))
        {
            topology.RecipesById[group.Key] = group.First().Recipe;
        }

        topology.InDegree = topology.RecipesById.Keys.ToDictionary(id => id, _ => 0);

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
                    topology.EdgeQty[key] = topology.EdgeQty.GetValueOrDefault(key) + producedPerCraft * child.RoundFactor;
                    topology.EdgeChannel[key] = ingredient.ItemOrTag;

                    topology.ProducersOf.TryAdd(parentRecipe.RecipeId, []);
                    if (topology.ProducersOf[parentRecipe.RecipeId].Add(child.RecipeId))
                    {
                        topology.InDegree[child.RecipeId] += 1;
                    }
                }

                var missing = coverage.GetMissingQuantity(ingredient);
                if (missing > Epsilon)
                {
                    var pk = (parentRecipe.RecipeId, ingredient.ItemOrTag.Id);
                    topology.PurchaseQty[pk] = topology.PurchaseQty.GetValueOrDefault(pk) + missing;
                    topology.PurchaseChannel[pk] = ingredient.ItemOrTag;
                }
            }
        }

        return topology;
    }

    // 2. Propagation du débit demandé (aval -> amont) sur le DAG des recettes, pour un jeu de débits
    //    cibles donné. Ne dépend de la topologie qu'en lecture seule (l'état mutable — degrés entrants,
    //    demande — est local), donc rejouable autant de fois que nécessaire (superposition « max »).
    private PropagationResult Propagate(AutomationTopology topology, DataContext shoppingList, IReadOnlyDictionary<Guid, decimal> seedRates)
    {
        var result = new PropagationResult();
        var demandRate = new Dictionary<(Guid Recipe, Guid Item), decimal>();
        var demandChannel = new Dictionary<(Guid Recipe, Guid Item), ItemOrTag>();
        var inDegree = new Dictionary<Guid, int>(topology.InDegree);

        void AddDemand(Guid recipeId, ItemOrTag channel, decimal rate)
        {
            var key = (recipeId, channel.Id);
            demandRate[key] = demandRate.GetValueOrDefault(key) + rate;
            demandChannel[key] = channel;
        }

        // Amorçage : la demande n'est portée QUE par les produits présents dans seedRates. Un produit
        // absent du jeu courant ne retombe PAS sur son débit par défaut : pendant la résolution par paliers
        // (cf. ResolveAutomation), chaque palier ne propage que ses propres cibles, et un produit d'un autre
        // palier (ex. une cible « max » pendant le palier neutre) doit rester à 0 — sinon il consommerait la
        // capacité d'entrée partagée et raboterait la résolution (une cible « max » tombait alors à 0 à cause
        // d'un produit neutre concurrent sur le même intrant). La propagation finale reçoit SeedRates complet
        // (tous les paliers résolus), donc aucun produit n'y manque.
        foreach (var rootRecipe in shoppingList.GetRootShoppingListRecipes())
        {
            foreach (var product in rootRecipe.Recipe.Elements.Where(e => e.IsProduct() && !e.DefaultIsReintegrated).OrderBy(e => e.Index))
            {
                var producedPerCraft = product.Quantity.GetDynamicValue(shoppingList);
                if (producedPerCraft <= Epsilon)
                {
                    continue;
                }

                if (!seedRates.TryGetValue(product.ItemOrTag.Id, out var rate))
                {
                    continue;
                }
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

            var recipe = topology.RecipesById[recipeId];

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
            result.TablesByRecipe[recipeId] = tables;

            // Répartition de la demande de chaque ingrédient vers ses producteurs (et l'achat).
            foreach (var ingredient in recipe.Elements.Where(e => e.IsIngredient()).OrderBy(e => e.Index))
            {
                var needRate = tables * PerMinuteRate(Math.Abs(ingredient.Quantity.GetDynamicValue(shoppingList)), recipe, shoppingList);
                if (needRate <= Epsilon)
                {
                    continue;
                }

                var channelId = ingredient.ItemOrTag.Id;
                var producerKeys = topology.EdgeQty.Keys.Where(k => k.Consumer == recipeId && k.Item == channelId).ToList();
                var totalQty = producerKeys.Sum(k => topology.EdgeQty[k]) + topology.PurchaseQty.GetValueOrDefault((recipeId, channelId));
                if (totalQty <= Epsilon)
                {
                    continue;
                }

                foreach (var key in producerKeys)
                {
                    var rate = needRate * topology.EdgeQty[key] / totalQty;
                    result.EdgeRate[key] = result.EdgeRate.GetValueOrDefault(key) + rate;
                    AddDemand(key.Producer, ingredient.ItemOrTag, rate);
                }

                var pk = (recipeId, channelId);
                if (topology.PurchaseQty.TryGetValue(pk, out var pq))
                {
                    result.PurchaseRate[pk] = result.PurchaseRate.GetValueOrDefault(pk) + needRate * pq / totalQty;
                }
            }

            if (topology.ProducersOf.TryGetValue(recipeId, out var children))
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
        foreach (var recipeId in topology.RecipesById.Keys)
        {
            Process(recipeId);
        }

        return result;
    }

    // Surplus de production : on pousse chaque atelier SOURCE (toutes ses entrées sont des achats de matière
    // première, aucune entrée intermédiaire) à monter en charge jusqu'à SATURER une de ses limites d'entrée.
    // Si, à ce régime, il produit plus que ce que l'aval consomme, l'excédent devient une sortie « surplus ».
    // Règles voulues : on vise toujours la limite d'entrée (production maximale) ; une entrée sans limite ou
    // déjà saturée ne déclenche aucun surplus. Les ateliers non-source (alimentés par un intermédiaire) ne
    // sont pas montés en charge : ce serait répercuter la demande en amont (hors périmètre de cet affichage).
    private SurplusPlan ComputeSurplusPlan(
        AutomationTopology topology,
        DataContext shoppingList,
        PropagationResult propagation,
        IReadOnlyDictionary<Guid, decimal> inputCaps)
    {
        var plan = new SurplusPlan();
        if (inputCaps.Count == 0)
        {
            return plan;
        }

        // Capacité d'entrée encore disponible par item (limite − achats déjà planifiés par la propagation).
        var leftover = inputCaps.ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var ((_, itemId), rate) in propagation.PurchaseRate)
        {
            if (leftover.ContainsKey(itemId))
            {
                leftover[itemId] = Math.Max(0m, leftover[itemId] - rate);
            }
        }

        var rootRecipeIds = shoppingList.GetRootShoppingListRecipes().Select(r => r.RecipeId).ToHashSet();

        foreach (var (recipeId, recipe) in topology.RecipesById)
        {
            // Atelier SOURCE (aucun producteur en amont) et NON final (ses sorties ne sont pas déjà des
            // produits finaux). Les autres restent dimensionnés sur la demande.
            if (rootRecipeIds.Contains(recipeId))
            {
                continue;
            }
            if (topology.ProducersOf.TryGetValue(recipeId, out var producers) && producers.Count > 0)
            {
                continue;
            }

            var tables = propagation.TablesByRecipe.GetValueOrDefault(recipeId);

            // Tables cibles : on monte jusqu'à ce qu'une entrée PLAFONNÉE sature sa limite. Le débit par table
            // de chaque entrée se lit sur la RECETTE (pas sur le régime courant), ce qui fonctionne même quand
            // l'atelier ne tourne pas encore (aval à 0) : il peut alors produire un surplus pur en tapant sa
            // limite. Entrée sans limite → ne contraint pas (ni ne pousse) ; entrée déjà saturée → pas de surplus.
            var targetTables = decimal.MaxValue;
            foreach (var ingredient in recipe.Elements.Where(e => e.IsIngredient()))
            {
                var itemId = ingredient.ItemOrTag.Id;
                if (!inputCaps.ContainsKey(itemId))
                {
                    continue;
                }
                var perTable = PerMinuteRate(Math.Abs(ingredient.Quantity.GetDynamicValue(shoppingList)), recipe, shoppingList);
                if (perTable <= Epsilon)
                {
                    continue;
                }
                // Capacité disponible pour CET atelier = restant global + ce qu'il consomme déjà (réallouable).
                var available = leftover.GetValueOrDefault(itemId) + propagation.PurchaseRate.GetValueOrDefault((recipeId, itemId));
                targetTables = Math.Min(targetTables, available / perTable);
            }

            if (targetTables == decimal.MaxValue || targetTables <= tables + Epsilon)
            {
                continue; // aucune entrée plafonnée à viser, ou déjà saturé → pas de surplus
            }

            var newTables = targetTables;
            plan.Tables[recipeId] = newTables;

            // Achats recalculés au régime maxé (débit/table × tables) + décompte de la capacité consommée.
            foreach (var ingredient in recipe.Elements.Where(e => e.IsIngredient()))
            {
                var itemId = ingredient.ItemOrTag.Id;
                var perTable = PerMinuteRate(Math.Abs(ingredient.Quantity.GetDynamicValue(shoppingList)), recipe, shoppingList);
                if (perTable <= Epsilon)
                {
                    continue;
                }
                var scaled = perTable * newTables;
                var current = propagation.PurchaseRate.GetValueOrDefault((recipeId, itemId));
                plan.Purchase[(recipeId, itemId)] = scaled;
                if (leftover.ContainsKey(itemId))
                {
                    leftover[itemId] = Math.Max(0m, leftover[itemId] - (scaled - current));
                }
            }

            // Consommation aval par item produit (inchangée par la montée en charge), pour en déduire l'excédent.
            var consumed = new Dictionary<Guid, decimal>();
            foreach (var (key, rate) in propagation.EdgeRate.Where(kv => kv.Key.Producer == recipeId))
            {
                if (rate <= Epsilon)
                {
                    continue;
                }
                var channel = topology.EdgeChannel[key];
                var suppliers = recipe.Elements
                    .Where(e => e.IsProduct() && !e.DefaultIsReintegrated
                        && ShoppingListCoverageCalculator.CanSupplyIngredient(e.ItemOrTag, channel))
                    .ToList();
                var totalQ = suppliers.Sum(s => s.Quantity.GetDynamicValue(shoppingList));
                if (totalQ <= Epsilon)
                {
                    continue;
                }
                foreach (var supplier in suppliers)
                {
                    consumed[supplier.ItemOrTag.Id] = consumed.GetValueOrDefault(supplier.ItemOrTag.Id)
                        + rate * supplier.Quantity.GetDynamicValue(shoppingList) / totalQ;
                }
            }

            // Production aux nouvelles tables, par item produit ; l'excédent (− consommation aval) part en surplus.
            var produced = new Dictionary<Guid, (decimal Rate, ItemOrTag Item)>();
            foreach (var product in recipe.Elements.Where(e => e.IsProduct() && !e.DefaultIsReintegrated))
            {
                var rate = newTables * PerMinuteRate(product.Quantity.GetDynamicValue(shoppingList), recipe, shoppingList);
                if (rate <= Epsilon)
                {
                    continue;
                }
                var prev = produced.GetValueOrDefault(product.ItemOrTag.Id);
                produced[product.ItemOrTag.Id] = (prev.Rate + rate, product.ItemOrTag);
            }

            foreach (var (itemId, entry) in produced)
            {
                var surplus = entry.Rate - consumed.GetValueOrDefault(itemId);
                if (surplus > Epsilon)
                {
                    plan.Surplus.Add((recipeId, entry.Item, surplus));
                }
            }
        }

        return plan;
    }

    // Demande totale par item d'entrée (somme des débits d'achat sur tous les consommateurs).
    private static Dictionary<Guid, decimal> DemandByInput(PropagationResult propagation)
    {
        var demand = new Dictionary<Guid, decimal>();
        foreach (var ((_, itemId), rate) in propagation.PurchaseRate)
        {
            demand[itemId] = demand.GetValueOrDefault(itemId) + rate;
        }
        return demand;
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

    // Partie du graphe d'automatisation indépendante des débits cibles (réutilisable entre propagations).
    private sealed class AutomationTopology
    {
        public Dictionary<Guid, Recipe> RecipesById { get; } = new();
        public Dictionary<(Guid Producer, Guid Consumer, Guid Item), decimal> EdgeQty { get; } = new();
        public Dictionary<(Guid Producer, Guid Consumer, Guid Item), ItemOrTag> EdgeChannel { get; } = new();
        public Dictionary<(Guid Consumer, Guid Item), decimal> PurchaseQty { get; } = new();
        public Dictionary<(Guid Consumer, Guid Item), ItemOrTag> PurchaseChannel { get; } = new();
        public Dictionary<Guid, HashSet<Guid>> ProducersOf { get; } = new();
        public Dictionary<Guid, int> InDegree { get; set; } = new();
    }

    // Plan de surplus : pour les ateliers source poussés à saturer leur limite d'entrée, le nombre de tables
    // majoré, les achats majorés (entrée tapée à la limite) et les sorties excédentaires (recette, item, débit).
    private sealed class SurplusPlan
    {
        public Dictionary<Guid, decimal> Tables { get; } = new();
        public Dictionary<(Guid Consumer, Guid Item), decimal> Purchase { get; } = new();
        public List<(Guid RecipeId, ItemOrTag Item, decimal Rate)> Surplus { get; } = new();
    }

    // Résultat d'une propagation : nombre de tables par recette, débits sur arêtes et débits d'achat.
    private sealed class PropagationResult
    {
        public Dictionary<Guid, decimal> TablesByRecipe { get; } = new();
        public Dictionary<(Guid Producer, Guid Consumer, Guid Item), decimal> EdgeRate { get; } = new();
        public Dictionary<(Guid Consumer, Guid Item), decimal> PurchaseRate { get; } = new();
    }

    // Débits résolus par priorité (fixe > neutre > max) : débit effectif amorçant la propagation finale,
    // cibles « max » illimitées (faute de limite contraignante) et entrées goulots (limite saturée ayant
    // raboté un palier fixe/neutre).
    private sealed class ResolvedAutomation
    {
        public Dictionary<Guid, decimal> SeedRates { get; } = new();
        public HashSet<Guid> UnboundedMax { get; } = new();
        public HashSet<Guid> BottleneckInputs { get; } = new();
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
    public ItemOrTag ItemOrTag { get; set; } = null!;
    public string Name { get; set; } = "";
    public decimal DefaultRate { get; set; }
}

// Résultat du planificateur : graphe à afficher + débits réellement produits par cible (pour réafficher les
// « max » résolus et détecter les objectifs fixes rabotés), la liste des cibles « max » non bornées
// (illimitées faute de contrainte d'entrée limitante) et les noms des items goulots (pour le tooltip).
public class AutomationGraphResult
{
    public ProductionGraphData Data { get; set; } = new();
    public Dictionary<Guid, decimal> ResolvedTargetRates { get; set; } = new();
    public HashSet<Guid> UnboundedMaxItems { get; set; } = new();
    public List<string> BottleneckItemNames { get; set; } = new();
}

public class AutomationInput
{
    public Guid ItemId { get; set; }
    public ItemOrTag ItemOrTag { get; set; } = null!;
    public string Name { get; set; } = "";
}
