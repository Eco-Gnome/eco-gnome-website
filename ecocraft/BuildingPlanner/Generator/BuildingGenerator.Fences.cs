using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Clôtures. Trois familles en jeu : FarEast et Adobe posent FenceMid, FenceCorner, FenceT, FenceX, FenceEnd et FenceSolo
// tournés par le joueur (mêmes conventions dans les deux sets) ; les pontons Hewn Log, les DocksFence* (autres conventions) ;
// acier ondulé, béton armé, acier plat, composites et pierres de taille, une forme Fence qui se raccorde seule. Les raccords
// viennent du tracé, pas du voisinage : deux clôtures parallèles côte à côte ne se soudent pas, un tracé qui part d'un
// autre fait un T. Un poteau (Column déjà émis) sur le tracé devient un montant : la clôture n'y est pas posée mais ses
// voisines gardent leur raccord vers lui, le rail court donc jusqu'au poteau. Rotations mesurées sur les meshes (voir le
// prompt système).
public static partial class BuildingGenerator
{
    private enum FenceFamily { None, Turned, Docks, Auto }

    private static FenceFamily FenceFamilyOf(string material)
        => IsFarEast(material) || IsAdobe(material) ? FenceFamily.Turned
        : material.EndsWith("HewnLogItem", StringComparison.Ordinal) ? FenceFamily.Docks
        : material is "CorrugatedSteelItem" or "ReinforcedConcreteItem" or "FlatSteelItem"
          || material.StartsWith("Composite", StringComparison.Ordinal) || material.StartsWith("Ashlar", StringComparison.Ordinal) ? FenceFamily.Auto
        : FenceFamily.None;

    // Pièce et rotation d'une case de clôture tournée d'après ses raccords (bit d = raccord vers dir(d) : est, sud, ouest, nord).
    private static (string Form, int Rot) TurnedFencePiece(int mask, bool docks)
    {
        var dirs = Enumerable.Range(0, 4).Where(d => (mask & 1 << d) != 0).ToList();
        bool east = (mask & 1) != 0, south = (mask & 2) != 0;
        return dirs.Count switch
        {
            0 => (docks ? "DocksFenceSolo" : "FenceSolo", 0),
            // Bout : le rail continue vers dir(d). FenceEnd : E, N, W, S pour rot 0..3 ; DocksFenceEndCap : S, E, N, W.
            1 => docks ? ("DocksFenceEndCap", dirs[0] ^ 1) : ("FenceEnd", LowToward(dirs[0])),
            // Segment : FenceMid rot 0 le long de x ; DocksFenceMid rot 0 le long de y.
            2 when dirs[1] - dirs[0] == 2 => (docks ? "DocksFenceMid" : "FenceMid", (dirs[0] == 0) == docks ? 1 : 0),
            // Angle : FenceCorner bras O+N, O+S, E+S, E+N pour rot 0..3 ; DocksFenceCorner E+S, E+N, O+N, O+S.
            2 => (docks ? "DocksFenceCorner" : "FenceCorner", docks ? (east ? (south ? 0 : 1) : (south ? 3 : 2)) : (east ? (south ? 2 : 3) : (south ? 1 : 0))),
            // T : branche vers N, O, S, E pour rot 0..3 dans les deux familles, comme WallT.
            3 => (docks ? "DocksFenceT" : "FenceT", TeeWallRot((Enumerable.Range(0, 4).First(d => (mask & 1 << d) == 0) + 2) % 4)),
            _ => (docks ? "DocksFenceX" : "FenceX", 0),
        };
    }

    private sealed partial class Build
    {
        public void EmitFences()
        {
            // Raccords par (matériau, baseZ, hauteur) : les tracés d'un même groupe se rejoignent en T ou en X.
            var groups = new Dictionary<(string Material, int BaseZ, int Height), (Dictionary<(int X, int Y), int> Links, HashSet<(int X, int Y)> Gates)>();
            foreach (var (f, i) in Take(P.Fences, "clôtures").Select((f, i) => (f, i)))
            {
                var what = $"clôture n° {i + 1}";
                var material = Material(f.Material, what);
                if (FenceFamilyOf(material) == FenceFamily.None) { Notes.Add($"{what} : pas de clôture en {material}, ignorée"); continue; }
                if (f.Points.Count == 0) { Notes.Add($"{what} : tracé vide, ignorée"); continue; }
                var points = f.Points.Select(p => (X: Math.Clamp(p.X, 0, W - 1), Y: Math.Clamp(p.Y, 0, D - 1))).ToList();
                if (points.Zip(f.Points).Any(z => z.First.X != z.Second.X || z.First.Y != z.Second.Y)) Notes.Add($"{what} : tracé ramené dans la grille");
                if (f.Closed && points.Count > 2 && points[^1] != points[0]) points.Add(points[0]);
                var key = (material, Math.Clamp(f.BaseZ, 0, MaxBaseZ), Math.Clamp(f.Height, 1, 4));
                if (!groups.TryGetValue(key, out var group)) groups[key] = group = (new(), []);
                var gates = f.Gates.Select(g => (g.X, g.Y)).ToHashSet();
                group.Gates.UnionWith(gates);

                var cur = points[0];
                group.Links.TryAdd(cur, 0);
                foreach (var target in points.Skip(1))
                {
                    if (target.X != cur.X && target.Y != cur.Y) Notes.Add($"{what} : segment en biais, tracé en L");
                    while (cur != target)
                    {
                        var d = target.X > cur.X ? 0 : target.X < cur.X ? 2 : target.Y > cur.Y ? 1 : 3;
                        var next = (X: cur.X + Dir[d].X, Y: cur.Y + Dir[d].Y);
                        group.Links.TryAdd(next, 0);
                        if (!gates.Contains(cur) && !gates.Contains(next)) { group.Links[cur] |= 1 << d; group.Links[next] |= 1 << (d + 2) % 4; }
                        cur = next;
                    }
                }
            }

            var posts = Doc.Architecture.Ops.Where(o => o is { Kind: "box", Subtract: false, Form: "Column", A: not null, B: not null }).ToList();
            bool IsPost(int x, int y, int z) => posts.Any(o => x >= Math.Min(o.A![0], o.B![0]) && x <= Math.Max(o.A[0], o.B[0])
                && y >= Math.Min(o.A[1], o.B[1]) && y <= Math.Max(o.A[1], o.B[1]) && z >= Math.Min(o.A[2], o.B[2]) && z <= Math.Max(o.A[2], o.B[2]));

            EmitCategory("clôtures", groups.Select(g =>
            {
                var (material, baseZ, height) = g.Key;
                var family = FenceFamilyOf(material);
                return g.Value.Links.Where(c => !g.Value.Gates.Contains(c.Key))
                    .GroupBy(c => family == FenceFamily.Auto ? ("Fence", 0) : TurnedFencePiece(c.Value, family == FenceFamily.Docks))
                    .Select(piece =>
                    {
                        var cells = new List<int>();
                        foreach (var c in piece)
                        for (var z = baseZ + 1; z <= baseZ + height; z++)
                            if (!IsPost(c.Key.X, c.Key.Y, z)) cells.AddRange([c.Key.X, c.Key.Y, z]);
                        return Cells(cells, material, piece.Key.Item1, piece.Key.Item2);
                    }).Where(op => op.Cells!.Length > 0).ToList();
            }));
        }
    }
}
