using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Coût en matériaux : voxels de bloc par matériau et usage (mur / sol / plafond) + objets par type.
public static class MaterialCostCalculator
{
    public static (List<MaterialCostLine> Materials, List<ObjectCostLine> Objects) Compute(BuildContext ctx)
    {
        var grid = ctx.Grid;
        var lines = new Dictionary<int, MaterialCostLine>();

        for (var y = 0; y < grid.SizeY; y++)
        for (var z = 0; z < grid.SizeZ; z++)
        for (var x = 0; x < grid.SizeX; x++)
        {
            var v = grid.Get(new Vec3i(x, y, z));
            if (v.Kind != VoxelKind.Block || v.MaterialIndex < 0) continue;
            if (!lines.TryGetValue(v.MaterialIndex, out var line))
            {
                line = new MaterialCostLine { Material = ctx.Materials[v.MaterialIndex].Name };
                lines[v.MaterialIndex] = line;
            }
            if (v.IsFloor) line.Floors++;
            else if (v.IsCeiling) line.Ceilings++;
            else line.Walls++;
        }

        var objects = ctx.Objects
            .Where(o => o.Placed && o.Info is not null)
            .GroupBy(o => o.Info!.Name)
            .Select(g => new ObjectCostLine { Type = g.Key, Count = g.Count() })
            .OrderByDescending(l => l.Count).ThenBy(l => l.Type)
            .ToList();

        return (lines.Values.OrderByDescending(l => l.Total).ThenBy(l => l.Material).ToList(), objects);
    }
}
