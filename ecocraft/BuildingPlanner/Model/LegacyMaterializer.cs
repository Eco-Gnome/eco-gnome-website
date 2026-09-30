namespace ecocraft.BuildingPlanner.Model;

// Copie figée de l'ancien GridBuilder (schéma 3, 72edacd) sans problèmes ni objets : les murs, sols et plafonds
// implicites d'un document v3 deviennent des cellules (x, y, z) → (matériau, forme), émises en ops `cells`. Ne pas
// faire évoluer : c'est ce qui garantit qu'un plan migré garde exactement ses blocs.
// Niveau k : barrière 2D = murs du document ∪ blocs de forme à base_k + 1 ; empreinte d'une pièce = flood fill
// 4-connexe depuis sa graine ; sol du niveau 0 = surcharges partout + matériau par défaut sous les pièces ; dalles
// explicites des étages ; murs base+1..base+h (écrasent) ; plafond par pièce à base + hauteur + 1, seulement là où rien
// n'est encore posé, hors ouvertures de l'étage.
public static class LegacyMaterializer
{
    public static List<ArchOp> Materialize(PlanDocument doc, IReadOnlyList<LegacyLevel> levels, LegacyDefaults defaults)
    {
        var cells = new Dictionary<(int X, int Y, int Z), (string Material, string Form)>();
        var levelCount = Math.Min(doc.Levels.Count, levels.Count);
        // Document que l'analyse refusera pour sa taille : rien à matérialiser (évaluation et flood fills non bornés sinon).
        if (PlanValidator.Validate(doc).Any(i => i.Code is "GridSizeInvalid" or "TooManyLevels" or "TooManyOps" or "ArchitectureTooComplex" or "GridTooLarge"))
            return [];

        var walls = new Dictionary<(int X, int Y), LegacyWall>[levelCount];
        var floors = new Dictionary<(int X, int Y), string>[levelCount];
        var holes = new HashSet<(int X, int Y)>[levelCount];
        var blocked = new Func<(int X, int Y), bool>[levelCount];
        var footprints = new Dictionary<string, HashSet<(int X, int Y)>>(StringComparer.Ordinal);
        var wallHeights = new Dictionary<(int Level, int X, int Y), int>();

        var archTop = 0;
        foreach (var op in doc.Architecture.Ops) archTop = Math.Max(archTop, Math.Min(ArchShapes.MaxZ(op) + 1, doc.Architecture.Height));
        var arch = ArchLayer.Evaluate(doc, archTop);

        for (var k = 0; k < levelCount; k++)
        {
            var level = levels[k];
            var baseY = doc.LevelBaseY(k);
            var levelHeight = doc.LevelHeight(k);

            walls[k] = new Dictionary<(int X, int Y), LegacyWall>();
            foreach (var (key, wall) in level.Walls)
                if (PlanKeys.TryParse(key, out var x, out var y) && PlanValidator.InGrid(doc, x, y) && !string.IsNullOrWhiteSpace(wall.Material)) walls[k][(x, y)] = wall;
            var levelWalls = walls[k];
            var firstAirY = baseY + 1;
            blocked[k] = c => levelWalls.ContainsKey(c) || arch.IsSolid(c.X, c.Y, firstAirY);
            floors[k] = new Dictionary<(int X, int Y), string>();
            foreach (var (key, material) in level.Floors)
                if (PlanKeys.TryParse(key, out var x, out var y) && PlanValidator.InGrid(doc, x, y) && !string.IsNullOrWhiteSpace(material)) floors[k][(x, y)] = material;
            holes[k] = [];
            if (k > 0)
                foreach (var key in level.Holes.Keys)
                    if (PlanKeys.TryParse(key, out var x, out var y) && PlanValidator.InGrid(doc, x, y) && !floors[k].ContainsKey((x, y))) holes[k].Add((x, y));

            foreach (var room in level.Rooms)
            {
                var seed = (room.Seed.X, room.Seed.Y);
                footprints[room.Id] = blocked[k](seed) ? [] : FloodFill2D(doc, blocked[k], seed);
            }

            foreach (var (cell, wall) in walls[k])
                wallHeights[(k, cell.X, cell.Y)] = Math.Max(1, wall.Height ?? AdjacentRoomHeight(doc, k, level, footprints, cell) ?? levelHeight);
        }

        // Sol du niveau 0 : surcharge par cellule où qu'elle soit ; le matériau par défaut ne couvre que les pièces du
        // niveau 0 (intérieur + anneau de murs) ; le reste est du terrain.
        if (levelCount > 0)
        {
            foreach (var (cell, material) in floors[0]) cells[(cell.X, cell.Y, 0)] = (material, "Floor");
            if (!string.IsNullOrWhiteSpace(defaults.FloorMaterial))
                foreach (var room in levels[0].Rooms)
                foreach (var cell in Covered(footprints[room.Id], blocked[0]))
                    if (PlanValidator.InGrid(doc, cell.X, cell.Y)) cells.TryAdd((cell.X, cell.Y, 0), (defaults.FloorMaterial, "Floor"));
        }

        // Dalles explicites des étages.
        for (var k = 1; k < levelCount; k++)
            foreach (var (cell, material) in floors[k]) cells[(cell.X, cell.Y, doc.LevelBaseY(k))] = (material, "Floor");

        // Murs (un mur plus haut que son niveau traverse la dalle).
        for (var k = 0; k < levelCount; k++)
        {
            var baseY = doc.LevelBaseY(k);
            foreach (var (cell, wall) in walls[k])
                for (var y = baseY + 1; y <= baseY + wallHeights[(k, cell.X, cell.Y)] && y < PlanValidator.MaxArchitectureHeight; y++)
                    cells[(cell.X, cell.Y, y)] = (wall.Material, "Wall");
        }

        // Plafonds : intérieur + barrières adjacentes (8-voisinage), seulement là où rien n'est posé, hors ouvertures de
        // l'étage ; pièces dans l'ordre du document (la première posée l'emporte).
        for (var k = 0; k < levelCount; k++)
        {
            var baseY = doc.LevelBaseY(k);
            foreach (var room in levels[k].Rooms)
            {
                var footprint = footprints[room.Id];
                var material = room.CeilingMaterial ?? defaults.CeilingMaterial;
                if (footprint.Count == 0 || string.IsNullOrWhiteSpace(material)) continue;
                var ceilingY = baseY + (room.Height ?? doc.LevelHeight(k)) + 1;
                if (ceilingY <= 0 || ceilingY >= PlanValidator.MaxArchitectureHeight) continue;
                var upperHoles = k + 1 < levelCount && ceilingY == doc.LevelBaseY(k + 1) ? holes[k + 1] : null;
                foreach (var cell in Covered(footprint, blocked[k]))
                    if (!(upperHoles?.Contains(cell) ?? false) && PlanValidator.InGrid(doc, cell.X, cell.Y)) cells.TryAdd((cell.X, cell.Y, ceilingY), (material, "Floor"));
            }
        }

        // Une op `cells` par (matériau, forme, couche), découpée au plafond du validateur.
        var ops = new List<ArchOp>();
        foreach (var group in cells.GroupBy(c => (c.Key.Z, c.Value.Material, c.Value.Form)).OrderBy(g => g.Key.Z).ThenBy(g => g.Key.Material, StringComparer.Ordinal).ThenBy(g => g.Key.Form, StringComparer.Ordinal))
        foreach (var chunk in group.Select(c => c.Key).OrderBy(c => c.Y).ThenBy(c => c.X).Chunk(PlanValidator.MaxCellsPerOp))
            ops.Add(new ArchOp
            {
                Id = $"h{ops.Count + 1}", Kind = "cells", Material = group.Key.Material, Form = group.Key.Form, Rot = 0,
                Cells = chunk.SelectMany(c => new[] { c.X, c.Y, c.Z }).ToArray(),
            });
        return ops;
    }

    // Empreinte de la pièce plus les barrières qui la touchent (8-voisinage) : ce que couvrent sa dalle et son plafond.
    private static HashSet<(int X, int Y)> Covered(HashSet<(int X, int Y)> footprint, Func<(int X, int Y), bool> blocked)
    {
        var covered = new HashSet<(int X, int Y)>(footprint);
        foreach (var cell in footprint)
        foreach (var (dx, dy) in Geometry.PlanNeighbors8)
        {
            var n = (cell.X + dx, cell.Y + dy);
            if (blocked(n)) covered.Add(n);
        }
        return covered;
    }

    // Flood fill 4-connexe dans le plan, barrières = murs et blocs de forme ; bornée par la grille.
    private static HashSet<(int X, int Y)> FloodFill2D(PlanDocument doc, Func<(int X, int Y), bool> blocked, (int X, int Y) seed)
    {
        var footprint = new HashSet<(int X, int Y)> { seed };
        var stack = new Stack<(int X, int Y)>();
        stack.Push(seed);
        while (stack.Count > 0)
        {
            var cell = stack.Pop();
            foreach (var (dx, dy) in Geometry.PlanNeighbors4)
            {
                var n = (X: cell.X + dx, Y: cell.Y + dy);
                if (!PlanValidator.InGrid(doc, n.X, n.Y) || blocked(n) || !footprint.Add(n)) continue;
                stack.Push(n);
            }
        }
        return footprint;
    }

    private static int? AdjacentRoomHeight(PlanDocument doc, int level, LegacyLevel legacy, Dictionary<string, HashSet<(int X, int Y)>> footprints, (int X, int Y) wall)
    {
        int? best = null;
        foreach (var room in legacy.Rooms)
        {
            var footprint = footprints[room.Id];
            if (footprint.Count == 0) continue;
            foreach (var (dx, dy) in Geometry.PlanNeighbors8)
            {
                if (!footprint.Contains((wall.X + dx, wall.Y + dy))) continue;
                var height = room.Height ?? doc.LevelHeight(level);
                best = best is null ? height : Math.Max(best.Value, height);
                break;
            }
        }
        return best;
    }
}
