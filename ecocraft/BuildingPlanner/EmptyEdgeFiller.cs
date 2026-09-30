using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// « Combler les arêtes vides » : l'air vu en diagonale depuis une pièce contenue (arête vide, RoomChecker) compte en tier 0 ;
// en jeu on le corrige en posant un bloc dans la cellule de la pièce qui le voit, l'extérieur ne change pas. Chaque cellule
// d'air de la pièce voisine en diagonale d'une arête vide reçoit un cube : matériau du bloc juste au-dessus, sinon le plus
// fréquent de ses 6 voisins (puis de ses 26), en blocs qui font mur (terrain exclu). La graine et les cellules déjà occupées
// (objet, tuyau) sont laissées. Une cellule comblée peut isoler de l'air : on reconstruit et on recommence tant qu'il en
// reste, au plus MaxPasses fois. Sortie : une op cells par matériau (≤ MaxCellsPerOp cellules), dans la limite de MaxOps.
public static class EmptyEdgeFiller
{
    public const int MaxPasses = 5;

    public sealed record Result(List<ArchOp> Ops, int Placed, int Skipped);

    private static readonly Vec3i[] Offsets6 = Geometry.Offsets26.Where(d => !d.IsDiagonal).ToArray();

    public static Result Fill(PlanDocument doc, Catalog catalog)
    {
        var work = PlanDocumentJson.Parse(PlanDocumentJson.Serialize(doc));   // copie : le document de la page ne bouge pas
        var filled = new Dictionary<Vec3i, string>();                          // cellule Eco → matériau
        var skipped = new HashSet<Vec3i>();
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var ctx = GridBuilder.Build(work, catalog);
            ObjectPlacer.PlaceAll(ctx);
            var fills = new Dictionary<Vec3i, string>();
            foreach (var (level, room) in work.AllRooms())
            {
                var seed = Geometry.PlanToEco(room.Seed.X, room.Seed.Y, work.LevelBaseY(level) + room.Seed.Z);
                var stats = RoomChecker.GetRoomStats(ctx, seed);
                if (!stats.Contained) continue;
                foreach (var edge in stats.EmptyEdges)
                foreach (var dir in Geometry.Offsets26)
                {
                    var cell = edge + dir;
                    if (!dir.IsDiagonal || cell == seed || fills.ContainsKey(cell) || !stats.EmptySpace.Contains(cell)) continue;
                    if (ctx.Grid.Get(cell).Kind != VoxelKind.Air) { skipped.Add(cell); continue; }
                    if (MaterialFor(ctx, cell) is { } material) fills[cell] = material;
                }
            }
            if (fills.Count == 0) break;
            foreach (var (cell, material) in fills) filled[cell] = material;
            work.Architecture.Ops.AddRange(CellsOps(fills, () => ""));
        }

        var used = doc.Architecture.Ops.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        var n = 0;
        string NextId() { string id; do id = "edge" + ++n; while (!used.Add(id)); return id; }
        var ops = CellsOps(filled, NextId).Take(Math.Max(0, PlanValidator.MaxOps - doc.Architecture.Ops.Count)).ToList();
        return new Result(ops, ops.Sum(o => o.Cells!.Length / 3), skipped.Count);
    }

    // Bloc du matériau du dessus, sinon le plus fréquent des voisins (à égalité, le premier par nom) ; seuls les blocs qui
    // font mur comptent : le cube posé doit fermer l'arête.
    private static string? MaterialFor(BuildContext ctx, Vec3i cell)
    {
        var above = ctx.Grid.Get(cell + new Vec3i(0, 1, 0));
        if (above.Kind == VoxelKind.Block && ctx.IsWallVoxel(above)) return ctx.Materials[above.MaterialIndex].Name;
        return MostFrequent(ctx, cell, Offsets6) ?? MostFrequent(ctx, cell, Geometry.Offsets26);
    }

    private static string? MostFrequent(BuildContext ctx, Vec3i cell, Vec3i[] dirs) => dirs
        .Select(d => ctx.Grid.Get(cell + d))
        .Where(v => v.Kind == VoxelKind.Block && ctx.IsWallVoxel(v))
        .GroupBy(v => ctx.Materials[v.MaterialIndex].Name)
        .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
        .FirstOrDefault()?.Key;

    // Une op cells (forme Cube) par matériau, découpée à MaxCellsPerOp ; coordonnées du plan (x, y, z vertical).
    private static IEnumerable<ArchOp> CellsOps(Dictionary<Vec3i, string> cells, Func<string> id) => cells
        .GroupBy(c => c.Value, StringComparer.Ordinal)
        .SelectMany(g => g.Select(c => c.Key).Chunk(PlanValidator.MaxCellsPerOp).Select(chunk => new ArchOp
        {
            Id = id(),
            Kind = "cells",
            Material = g.Key,
            Form = "Cube",
            Cells = chunk.SelectMany(c => new[] { c.X, c.Z, c.Y }).ToArray(),   // Eco → plan
        }));
}
