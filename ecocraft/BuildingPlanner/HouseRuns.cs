namespace ecocraft.BuildingPlanner;

// Voxels de la maison (murs, sols, plafonds) pour le canvas du mode architecture, qui les compose lui-même avec les
// formes : segments [z, y, x0, longueur, matériau] par ligne, en coordonnées du plan (z vertical, y = ligne),
// matériau = index dans BuildContext.Materials. Compact : un sol ou un plafond fait un segment par ligne.
public static class HouseRuns
{
    public static List<int[]> Encode(VoxelGrid grid)
    {
        var runs = new List<int[]>();
        for (var y = 0; y < grid.SizeY; y++)
        for (var z = 0; z < grid.SizeZ; z++)
        {
            var start = -1;
            var material = -1;
            for (var x = 0; x <= grid.SizeX; x++)
            {
                var v = x < grid.SizeX ? grid.Get(new Vec3i(x, y, z)) : Voxel.Air;
                var m = v.Kind == VoxelKind.Block && v.MaterialIndex >= 0 ? v.MaterialIndex : -1;
                if (m == material) continue;
                if (material >= 0) runs.Add([y, z, start, x - start, material]);
                start = x;
                material = m;
            }
        }
        return runs;
    }
}
