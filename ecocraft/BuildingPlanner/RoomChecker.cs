using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

public sealed class RoomStats
{
    public bool Contained { get; set; }
    public string? FailCode { get; set; }
    public Vec3i? FailCell { get; set; }
    public HashSet<Vec3i> EmptySpace { get; } = [];
    public HashSet<Vec3i> Walls { get; } = [];
    public int WallCount { get; set; }
    public Dictionary<float, int> WallTierComposition { get; } = new();
    public int EmptyEdgeCount { get; set; }
    public float AverageTier { get; set; }
    public Dictionary<int, int> ObjectCellHits { get; } = new();   // index d'objet → cellules vues pendant la détection
    public int Volume => EmptySpace.Count;                          // pas de fenêtres en v1

    public float AverageTierExcludingEmptyEdges()
    {
        var count = WallTierComposition.Sum(x => x.Value) - EmptyEdgeCount;
        if (count <= 0) return 0f;
        var sum = WallTierComposition.Sum(x => x.Key * x.Value);   // les arêtes vides sont à 0 : la somme ne change pas
        return EcoMath.RoundF2(sum / count);
    }
}

// Réplique de Eco.Gameplay.Property.RoomChecker.GetRoomStats (AllowEmptyEdges = true, pas de fenêtres, pas d'eau) :
// flood fill LIFO depuis la graine sur les 26 voisins ; l'air en diagonale n'est pas propagé mais compté en arête
// vide (tier 0) ; les murs en diagonale comptent ; échec si une cellule dépasse MaxRoomDistance de la graine ou
// n'a aucun solide au-dessus d'elle ; volume ≤ 2 rejeté.
public static class RoomChecker
{
    public static RoomStats GetRoomStats(BuildContext ctx, Vec3i seed)
    {
        var grid = ctx.Grid;
        var stats = new RoomStats();

        if (ctx.IsWallVoxel(grid.Get(seed))) { stats.FailCode = "RoomSeedInWall"; stats.FailCell = seed; return stats; }

        var open = new Stack<Vec3i>();
        var visited = new HashSet<Vec3i>();
        var emptyEdges = new HashSet<Vec3i>();
        open.Push(seed);
        stats.EmptySpace.Add(seed);

        while (open.Count > 0)
        {
            var spot = open.Pop();
            if (Geometry.Distance(spot, seed) > ctx.Catalog.Build.MaxRoomDistance) { stats.FailCode = "RoomTooBig"; stats.FailCell = spot; return stats; }
            if (spot.Y >= grid.TopSolidY(spot.X, spot.Z)) { stats.FailCode = "NoCeiling"; stats.FailCell = spot; return stats; }

            foreach (var dir in Geometry.Offsets26)
            {
                var pos = spot + dir;
                if (visited.Contains(pos)) continue;

                var voxel = grid.Get(pos);
                if (dir.IsDiagonal && voxel.Kind == VoxelKind.Air) { emptyEdges.Add(pos); continue; }

                visited.Add(pos);

                if (voxel.IsObject && voxel.ObjectIndex >= 0)
                    stats.ObjectCellHits[voxel.ObjectIndex] = stats.ObjectCellHits.GetValueOrDefault(voxel.ObjectIndex) + 1;

                if (ctx.IsWallVoxel(voxel))
                {
                    if (stats.Walls.Add(pos))
                    {
                        stats.WallCount++;
                        var tier = ctx.WallTier(voxel);
                        if (tier is { } t) stats.WallTierComposition[t] = stats.WallTierComposition.GetValueOrDefault(t) + 1;
                    }
                }
                else
                {
                    stats.EmptySpace.Add(pos);
                    open.Push(pos);
                }
            }
        }

        if (stats.Volume <= 2) { stats.FailCode = "VolumeTooSmall"; stats.FailCell = seed; return stats; }

        emptyEdges.ExceptWith(stats.EmptySpace);
        stats.EmptyEdgeCount = emptyEdges.Count;
        if (emptyEdges.Count > 0) stats.WallTierComposition[0f] = stats.WallTierComposition.GetValueOrDefault(0f) + emptyEdges.Count;

        var total = stats.WallTierComposition.Sum(x => x.Value);
        stats.AverageTier = total > 0 ? EcoMath.RoundF2(stats.WallTierComposition.Sum(x => x.Key * x.Value) / (float)total) : 0f;
        stats.Contained = true;
        return stats;
    }

    // IsValidContainedObject : au moins 51 % des cellules posées vues par la détection (50/50 → aucune pièce).
    public static bool IsContained(PlacedObject obj, RoomStats stats, float requiredRatio = 0.51f)
    {
        var placed = obj.Cells.Count;
        if (placed == 0) return false;
        var hits = stats.ObjectCellHits.GetValueOrDefault(obj.Index);
        if (hits == 0) return false;
        return hits >= placed * requiredRatio;
    }
}
