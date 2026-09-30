using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Ops du plan évaluées dans l'ordre : par cellule, rien / bloc d'un matériau / air creusé par une soustraction.
// GridBuilder la fond dans la grille par-dessus le terrain (une soustraction à z = 0 creuse le terrain).
// Coordonnées du plan : (x, y, z) avec z vertical, rognées à Architecture.Height comme evalOps côté JS.
public sealed class ArchLayer
{
    private const short Untouched = -1;
    private const short Carved = -2;

    private readonly short[] _cells;
    private readonly List<string> _palette = [];
    private readonly int _w, _d, _h;

    private ArchLayer(int w, int d, int h)
    {
        _w = w; _d = d; _h = h;
        _cells = new short[w * d * h];
        Array.Fill(_cells, Untouched);
    }

    public static ArchLayer Evaluate(PlanDocument doc, int h)
    {
        var layer = new ArchLayer(doc.Grid.Width, doc.Grid.Depth, Math.Max(0, Math.Min(h, doc.Architecture.Height)));
        var index = new Dictionary<string, short>(StringComparer.Ordinal);
        foreach (var op in doc.Architecture.Ops)
        {
            var value = Carved;
            if (!op.Subtract)
            {
                var name = op.Material ?? "";
                if (!index.TryGetValue(name, out value)) { value = (short)layer._palette.Count; layer._palette.Add(name); index[name] = value; }
            }
            ArchShapes.Paint(op, layer._w, layer._d, layer._h, (x, y, z) => layer._cells[layer.Index(x, y, z)] = value);
        }
        return layer;
    }

    public bool IsSolid(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < _w && y < _d && z < _h && _cells[Index(x, y, z)] >= 0;

    // Fond les formes dans la grille : ajout = bloc du matériau, soustraction = air.
    public void MergeInto(BuildContext ctx)
    {
        var grid = ctx.Grid;
        var materials = _palette.Select(ctx.GetOrAddMaterial).ToArray();
        for (var z = 0; z < _h && z < grid.SizeY; z++)
        for (var y = 0; y < _d; y++)
        for (var x = 0; x < _w; x++)
        {
            var v = _cells[Index(x, y, z)];
            if (v == Untouched) continue;
            grid.Set(new Vec3i(x, z, y), v == Carved ? Voxel.Air : new Voxel { Kind = VoxelKind.Block, MaterialIndex = materials[v], ObjectIndex = -1 });
        }
    }

    private int Index(int x, int y, int z) => x + _w * (y + _d * z);
}
