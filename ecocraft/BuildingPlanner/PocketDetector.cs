namespace ecocraft.BuildingPlanner;

// Poches d'air fermées de la grille, même spécification que detectPockets côté JS : flood fill 6-connexe des voxels
// non-mur (les cellules d'objet [Occupied] sont traversées, comme dans RoomChecker ; les portes sont des murs), une
// composante est ouverte si elle touche l'extérieur de la grille au-dessus du terrain (Y > 0). Seules les poches de
// plus de 2 cellules sont gardées. Graine = cellule de la couche la plus basse la plus proche du centroïde de cette
// couche, à égalité le plus petit index x + W·y (y d'abord, puis x) comme le JS. Coordonnées du plan (x, y, z absolu).
// Utilisé par gen-bench ; le canvas a sa propre copie.
public static class PocketDetector
{
    public sealed record Pocket(int Label, int Count, int MinZ, (int X, int Y, int Z) Seed);

    public sealed class Result(VoxelGrid grid, int[] labels, List<Pocket> pockets)
    {
        public List<Pocket> Pockets { get; } = pockets;

        // Numéro de la poche fermée qui contient la cellule du plan, 0 sinon (mur, air ouvert, poche trop petite, hors grille).
        public int LabelAt(int x, int y, int z)
        {
            var p = Geometry.PlanToEco(x, y, z);
            return grid.InBounds(p) ? Math.Max(0, labels[grid.Index(p)]) : 0;
        }
    }

    public static Result Detect(BuildContext ctx)
    {
        var grid = ctx.Grid;
        var labels = new int[grid.SizeX * grid.SizeY * grid.SizeZ];   // 0 à voir, −1 mur / ouvert / trop petit, n poche
        var pockets = new List<Pocket>();
        var cells = new List<Vec3i>();
        var stack = new Stack<Vec3i>();
        for (var y = 0; y < grid.SizeY; y++)
        for (var z = 0; z < grid.SizeZ; z++)
        for (var x = 0; x < grid.SizeX; x++)
        {
            var start = new Vec3i(x, y, z);
            var index = grid.Index(start);
            if (labels[index] != 0) continue;
            if (ctx.IsWallVoxel(grid.Get(start))) { labels[index] = -1; continue; }

            var label = pockets.Count + 1;
            var open = false;
            cells.Clear();
            labels[index] = label;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                cells.Add(c);
                foreach (var d in Directions)
                {
                    var n = c + d;
                    if (!grid.InBounds(n)) { open |= n.Y > 0; continue; }
                    var i = grid.Index(n);
                    if (labels[i] != 0 || ctx.IsWallVoxel(grid.Get(n))) continue;
                    labels[i] = label;
                    stack.Push(n);
                }
            }

            if (open || cells.Count <= 2)
            {
                foreach (var c in cells) labels[grid.Index(c)] = -1;
                continue;
            }
            var minY = cells.Min(c => c.Y);
            var bottom = cells.Where(c => c.Y == minY).ToList();
            double cx = bottom.Average(c => c.X), cz = bottom.Average(c => c.Z);
            var seed = bottom.MinBy(c => ((c.X - cx) * (c.X - cx) + (c.Z - cz) * (c.Z - cz), c.Z, c.X));
            pockets.Add(new Pocket(label, cells.Count, minY, (seed.X, seed.Z, seed.Y)));
        }
        return new Result(grid, labels, pockets);
    }

    private static readonly Vec3i[] Directions = [new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)];
}
