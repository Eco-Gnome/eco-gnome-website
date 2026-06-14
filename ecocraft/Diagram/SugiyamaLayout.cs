using Blazor.Diagrams.Core.Geometry;

namespace ecocraft.Diagram;

// Algorithme de placement hiérarchique (Sugiyama) extrait de GraphView pour être réutilisé
// par les différentes vues graphe (exploration globale + chaîne de production de la shopping list).
// Place les sources (sans prédécesseur) en couche 0 puis chaque consommateur dans la couche
// suivante, ce qui donne un flux producteur → consommateur de gauche à droite.
public static class SugiyamaLayout
{
    public static void Apply(List<EcoNode> nodes, List<EcoEdge> edges, double layerSpacing = 400, double nodeSpacing = 100, int iterations = 200)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        RemoveCycles(nodes, edges);
        AssignLayers(nodes, edges);
        OrderNodesInLayers(nodes, edges, iterations);
        AssignCoordinates(nodes, layerSpacing, nodeSpacing);
    }

    private static void RemoveCycles(List<EcoNode> allNodes, List<EcoEdge> edges)
    {
        var visited = new HashSet<EcoNode>();
        var stack = new HashSet<EcoNode>();

        foreach (var node in allNodes)
        {
            if (!visited.Contains(node))
            {
                DFS(node, edges, visited, stack);
            }
        }
    }

    private static void DFS(EcoNode node, List<EcoEdge> edges, HashSet<EcoNode> visited, HashSet<EcoNode> stack)
    {
        visited.Add(node);
        stack.Add(node);

        foreach (var edge in GetOutgoingEdges(node, edges))
        {
            var neighbor = edge.Target;
            if (!visited.Contains(neighbor))
            {
                DFS(neighbor, edges, visited, stack);
            }
            else if (stack.Contains(neighbor))
            {
                // Cycle détecté, inverser l'arête
                edge.IsReversed = true;
                edge.Source = neighbor;
                edge.Target = node;
            }
        }

        stack.Remove(node);
    }

    private static List<EcoEdge> GetOutgoingEdges(EcoNode node, List<EcoEdge> edges)
    {
        return edges.Where(e => e.Source == node && !e.IsReversed).ToList();
    }

    private static List<EcoEdge> GetIncomingEdges(EcoNode node, List<EcoEdge> edges)
    {
        return edges.Where(e => e.Target == node && !e.IsReversed).ToList();
    }

    private static void AssignLayers(List<EcoNode> allNodes, List<EcoEdge> edges)
    {
        foreach (var node in allNodes)
        {
            node.Layer = -1; // Non assigné
        }

        // Trouver les nœuds sans prédécesseurs (sources)
        var sources = allNodes.Where(n => GetIncomingEdges(n, edges).Count == 0).ToList();

        var queue = new Queue<EcoNode>();
        foreach (var source in sources)
        {
            source.Layer = 0;
            queue.Enqueue(source);
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            int layer = node.Layer;

            foreach (var edge in GetOutgoingEdges(node, edges))
            {
                var neighbor = edge.Target;
                int neighborLayer = layer + 1;
                if (neighbor.Layer < neighborLayer)
                {
                    neighbor.Layer = neighborLayer;
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Garde-fou : un nœud isolé (aucune arête) ou non atteint reste en couche 0.
        foreach (var node in allNodes.Where(n => n.Layer < 0))
        {
            node.Layer = 0;
        }
    }

    private static void OrderNodesInLayers(List<EcoNode> allNodes, List<EcoEdge> edges, int iterations)
    {
        var layers = allNodes.GroupBy(n => n.Layer).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var layer in layers.Values)
        {
            int order = 0;
            foreach (var node in layer)
            {
                node.EcoOrder = order++;
            }
        }

        var orderedLayerKeys = layers.Keys.OrderBy(k => k).ToList();

        for (int i = 0; i < iterations; i++)
        {
            // Du haut vers le bas
            for (int l = 1; l < orderedLayerKeys.Count; l++)
            {
                var key = orderedLayerKeys[l];
                foreach (var node in layers[key])
                {
                    var incomingNodes = GetIncomingEdges(node, edges).Select(e => e.Source).ToList();
                    if (incomingNodes.Count > 0)
                    {
                        node.EcoOrder = incomingNodes.Average(n => n.EcoOrder);
                    }
                }

                ReassignOrders(layers, key);
            }

            // Du bas vers le haut
            for (int l = orderedLayerKeys.Count - 2; l >= 0; l--)
            {
                var key = orderedLayerKeys[l];
                foreach (var node in layers[key])
                {
                    var outgoingNodes = GetOutgoingEdges(node, edges).Select(e => e.Target).ToList();
                    if (outgoingNodes.Count > 0)
                    {
                        node.EcoOrder = outgoingNodes.Average(n => n.EcoOrder);
                    }
                }

                ReassignOrders(layers, key);
            }
        }
    }

    private static void ReassignOrders(Dictionary<int, List<EcoNode>> layers, int key)
    {
        layers[key] = layers[key].OrderBy(n => n.EcoOrder).ToList();
        int order = 0;
        foreach (var node in layers[key])
        {
            node.EcoOrder = order++;
        }
    }

    private static void AssignCoordinates(List<EcoNode> allNodes, double layerSpacing, double nodeSpacing)
    {
        var layers = allNodes.GroupBy(n => n.Layer).OrderBy(g => g.Key).ToList();

        foreach (var layerGroup in layers)
        {
            int layer = layerGroup.Key;
            var nodesInLayer = layerGroup.OrderBy(n => n.EcoOrder).ToList();

            // Le numéro de couche pilote l'axe horizontal (flux gauche → droite),
            // l'ordre dans la couche pilote l'axe vertical.
            double x = layer * layerSpacing;

            for (int i = 0; i < nodesInLayer.Count; i++)
            {
                double y = i * nodeSpacing;
                nodesInLayer[i].Position = new Point(x, y);
            }
        }
    }
}
