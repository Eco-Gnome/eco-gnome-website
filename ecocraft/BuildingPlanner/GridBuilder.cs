using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Document par niveaux → grille voxel. Niveau k : dalle à Y = base_k (niveau 0 : sol partout ; étages : cellules
// de sol explicites), murs Y = base_k + 1..h, plafond par pièce à Y = base_k + hauteur + 1 couvrant l'intérieur
// et l'anneau de murs, seulement là où il y a de l'air — un sol peint à l'étage l'emporte donc sur le plafond
// de la pièce du dessous, et les ouvertures de l'étage restent de l'air. Les objets sont posés ensuite.
public static class GridBuilder
{
    public static BuildContext Build(PlanDocument doc, Catalog catalog)
    {
        var levelCount = doc.Levels.Count;
        var walls = new Dictionary<(int X, int Y), WallCell>[levelCount];
        var floors = new Dictionary<(int X, int Y), string>[levelCount];
        var holes = new HashSet<(int X, int Y)>[levelCount];
        var footprints = new Dictionary<string, HashSet<(int X, int Y)>>(StringComparer.Ordinal);
        var wallHeights = new Dictionary<(int Level, int X, int Y), int>();
        var issues = new List<PlanIssue>();
        var sizeY = 3;

        for (var k = 0; k < levelCount; k++)
        {
            var level = doc.Levels[k];
            var baseY = doc.LevelBaseY(k);
            var levelHeight = doc.LevelHeight(k);

            walls[k] = new Dictionary<(int X, int Y), WallCell>();
            foreach (var (key, wall) in level.Walls)
                if (PlanKeys.TryParse(key, out var x, out var y) && PlanValidator.InGrid(doc, x, y)) walls[k][(x, y)] = wall;
            floors[k] = new Dictionary<(int X, int Y), string>();
            foreach (var (key, material) in level.Floors)
                if (PlanKeys.TryParse(key, out var x, out var y) && PlanValidator.InGrid(doc, x, y) && !string.IsNullOrWhiteSpace(material)) floors[k][(x, y)] = material;
            holes[k] = [];
            if (k > 0)
                foreach (var key in level.Holes.Keys)
                    if (PlanKeys.TryParse(key, out var x, out var y) && PlanValidator.InGrid(doc, x, y) && !floors[k].ContainsKey((x, y))) holes[k].Add((x, y));

            var footprintOwner = new Dictionary<(int X, int Y), string>();
            foreach (var room in level.Rooms)
            {
                var seed = (room.Seed.X, room.Seed.Y);
                if (walls[k].ContainsKey(seed)) { footprints[room.Id] = []; issues.Add(PlanIssue.Error("RoomSeedInWall", [room.Name], new GridPoint { X = seed.Item1, Y = seed.Item2 }, roomId: room.Id, level: k)); continue; }

                var (footprint, enclosed) = FloodFill2D(doc, walls[k], seed);
                footprints[room.Id] = footprint;
                if (!enclosed) issues.Add(PlanIssue.Warning("RoomNotEnclosed2D", [room.Name], new GridPoint { X = seed.Item1, Y = seed.Item2 }, roomId: room.Id, level: k));
                if (footprintOwner.TryGetValue(seed, out var otherRoom)) issues.Add(PlanIssue.Warning("RoomsShareSpace", [room.Name, level.Rooms.First(r => r.Id == otherRoom).Name], roomId: room.Id, level: k));
                else foreach (var cell in footprint) footprintOwner.TryAdd(cell, room.Id);

                var roomHeight = doc.RoomHeight(k, room);
                if (roomHeight > levelHeight) issues.Add(PlanIssue.Warning("RoomTallerThanLevel", [room.Name, roomHeight.ToString(), levelHeight.ToString()], roomId: room.Id, level: k));
                sizeY = Math.Max(sizeY, baseY + roomHeight + 1);
            }

            // Hauteur de chaque cellule de mur : surcharge, sinon plus haute pièce adjacente (8-voisinage), sinon niveau.
            foreach (var (cell, wall) in walls[k])
            {
                var height = Math.Max(1, wall.Height ?? AdjacentRoomHeight(doc, k, footprints, cell) ?? levelHeight);
                wallHeights[(k, cell.X, cell.Y)] = height;
                sizeY = Math.Max(sizeY, baseY + height);
            }
            sizeY = Math.Max(sizeY, baseY + levelHeight + 1);

            // Un objet plus haut que les murs dépasse simplement (comme dans le jeu) : la grille doit le contenir pour
            // que l'erreur remontée soit le plafond de la pièce, pas la taille de la grille interne.
            foreach (var o in level.Objects)
            {
                var info = catalog.GetObject(o.Type);
                if (info is null || info.Cells.Count == 0) continue;
                sizeY = Math.Max(sizeY, baseY + (o.Z ?? 1) + info.Cells.Max(c => c.Offset.Y) - Math.Min(0, info.Cells.Min(c => c.Offset.Y)));
            }
        }
        sizeY += 2;

        var grid = new VoxelGrid(doc.Grid.Width, sizeY, doc.Grid.Depth);
        var ctx = new BuildContext { Document = doc, Catalog = catalog, Grid = grid };
        ctx.Issues.AddRange(issues);
        if (doc.Levels.All(l => l.Rooms.Count == 0) && doc.Levels.Any(l => l.Objects.Count > 0)) ctx.Issues.Add(PlanIssue.Warning("NoRoomDefined", []));
        foreach (var (id, footprint) in footprints) ctx.RoomFootprints[id] = footprint;
        foreach (var (k, room) in doc.AllRooms()) ctx.RoomLevel[room.Id] = k;

        // Sol du niveau 0 : surcharge par cellule, sinon matériau par défaut, sinon terrain — partout, murs compris.
        var defaultFloorIndex = string.IsNullOrWhiteSpace(doc.Defaults.FloorMaterial) ? -1 : ctx.GetOrAddMaterial(doc.Defaults.FloorMaterial);
        for (var y = 0; y < doc.Grid.Depth; y++)
        for (var x = 0; x < doc.Grid.Width; x++)
        {
            var materialIndex = floors[0].TryGetValue((x, y), out var m) ? ctx.GetOrAddMaterial(m) : defaultFloorIndex;
            grid.Set(new Vec3i(x, 0, y), materialIndex < 0 ? Voxel.Terrain : new Voxel { Kind = VoxelKind.Block, MaterialIndex = materialIndex, ObjectIndex = -1, IsFloor = true });
        }

        // Dalles explicites des étages.
        for (var k = 1; k < levelCount; k++)
        {
            var baseY = doc.LevelBaseY(k);
            foreach (var (cell, material) in floors[k])
                grid.Set(new Vec3i(cell.X, baseY, cell.Y), new Voxel { Kind = VoxelKind.Block, MaterialIndex = ctx.GetOrAddMaterial(material), ObjectIndex = -1, IsFloor = true });
        }

        // Murs (un mur plus haut que son niveau traverse la dalle : bloc de mur, comme en jeu).
        for (var k = 0; k < levelCount; k++)
        {
            var baseY = doc.LevelBaseY(k);
            foreach (var (cell, wall) in walls[k])
            {
                var materialIndex = ctx.GetOrAddMaterial(wall.Material);
                var height = wallHeights[(k, cell.X, cell.Y)];
                for (var y = baseY + 1; y <= baseY + height && y < sizeY; y++)
                    grid.Set(new Vec3i(cell.X, y, cell.Y), new Voxel { Kind = VoxelKind.Block, MaterialIndex = materialIndex, ObjectIndex = -1 });
            }
        }

        // Plafonds : intérieur + murs adjacents (8-voisinage), seulement là où il y a de l'air, hors ouvertures de l'étage.
        for (var k = 0; k < levelCount; k++)
        {
            var baseY = doc.LevelBaseY(k);
            foreach (var room in doc.Levels[k].Rooms)
            {
                var footprint = footprints[room.Id];
                if (footprint.Count == 0) continue;
                var ceilingY = baseY + doc.RoomHeight(k, room) + 1;
                ctx.RoomCeilingY[room.Id] = ceilingY;

                var material = room.CeilingMaterial ?? doc.Defaults.CeilingMaterial;
                if (string.IsNullOrWhiteSpace(material)) { ctx.Issues.Add(PlanIssue.Warning("MissingCeilingMaterial", [room.Name], roomId: room.Id, level: k)); continue; }
                var materialIndex = ctx.GetOrAddMaterial(material);
                var upperHoles = k + 1 < levelCount && ceilingY == doc.LevelBaseY(k + 1) ? holes[k + 1] : null;

                var conflictReported = false;
                foreach (var cell in Covered(footprint, walls[k]))
                {
                    if (upperHoles is not null && upperHoles.Contains(cell)) continue;
                    var pos = new Vec3i(cell.X, ceilingY, cell.Y);
                    if (!grid.InBounds(pos)) continue;
                    var existing = grid.Get(pos);
                    if (existing.Kind == VoxelKind.Air) grid.Set(pos, new Voxel { Kind = VoxelKind.Block, MaterialIndex = materialIndex, ObjectIndex = -1, IsCeiling = true });
                    else if (existing.Kind == VoxelKind.Block && existing.IsCeiling && existing.MaterialIndex != materialIndex && !conflictReported)
                    {
                        ctx.Issues.Add(PlanIssue.Info("CeilingConflict", [room.Name, ctx.Materials[existing.MaterialIndex].Name], new GridPoint { X = cell.X, Y = cell.Y }, roomId: room.Id, level: k));
                        conflictReported = true;
                    }
                }
            }
        }

        for (var k = 0; k < levelCount; k++)
        {
            var levelFootprints = doc.Levels[k].Rooms.Select(r => footprints[r.Id]).ToList();
            DetectDiagonalGaps(k, walls[k], levelFootprints, ctx);
        }

        // Étage sans dalle sous une pièce (sol et anneau de murs) : la pièce fuit vers le niveau du dessous ou perd du tier.
        for (var k = 1; k < levelCount; k++)
        {
            var baseY = doc.LevelBaseY(k);
            foreach (var room in doc.Levels[k].Rooms)
            {
                var footprint = footprints[room.Id];
                if (footprint.Count == 0) continue;
                var missing = 0;
                (int X, int Y)? first = null;
                foreach (var cell in Covered(footprint, walls[k]))
                {
                    if (holes[k].Contains(cell) || grid.Get(new Vec3i(cell.X, baseY, cell.Y)).Kind != VoxelKind.Air) continue;
                    missing++;
                    first ??= cell;
                }
                if (missing > 0) ctx.Issues.Add(PlanIssue.Warning("MissingFloor", [room.Name, missing.ToString()], new GridPoint { X = first!.Value.X, Y = first.Value.Y }, roomId: room.Id, level: k));
            }
        }

        grid.RecomputeTopSolid();
        return ctx;
    }

    // Empreinte de la pièce plus les murs qui la touchent (8-voisinage) : ce que couvrent sa dalle et son plafond.
    private static HashSet<(int X, int Y)> Covered(HashSet<(int X, int Y)> footprint, Dictionary<(int X, int Y), WallCell> walls)
    {
        var covered = new HashSet<(int X, int Y)>(footprint);
        foreach (var cell in footprint)
        foreach (var (dx, dy) in Geometry.PlanNeighbors8)
        {
            var n = (cell.X + dx, cell.Y + dy);
            if (walls.ContainsKey(n)) covered.Add(n);
        }
        return covered;
    }

    // Flood fill 4-connexe dans le plan, barrières = murs ; sortir de la grille = pièce non fermée (on continue
    // pour avoir l'empreinte complète).
    private static (HashSet<(int X, int Y)> Footprint, bool Enclosed) FloodFill2D(PlanDocument doc, Dictionary<(int X, int Y), WallCell> walls, (int X, int Y) seed)
    {
        var footprint = new HashSet<(int X, int Y)>();
        var stack = new Stack<(int X, int Y)>();
        var enclosed = true;
        stack.Push(seed);
        footprint.Add(seed);

        while (stack.Count > 0)
        {
            var cell = stack.Pop();
            foreach (var (dx, dy) in Geometry.PlanNeighbors4)
            {
                var n = (X: cell.X + dx, Y: cell.Y + dy);
                if (!PlanValidator.InGrid(doc, n.X, n.Y)) { enclosed = false; continue; }
                if (walls.ContainsKey(n) || !footprint.Add(n)) continue;
                stack.Push(n);
            }
        }

        return (footprint, enclosed);
    }

    private static int? AdjacentRoomHeight(PlanDocument doc, int level, Dictionary<string, HashSet<(int X, int Y)>> footprints, (int X, int Y) wall)
    {
        int? best = null;
        foreach (var room in doc.Levels[level].Rooms)
        {
            var footprint = footprints[room.Id];
            if (footprint.Count == 0) continue;
            foreach (var (dx, dy) in Geometry.PlanNeighbors8)
            {
                if (!footprint.Contains((wall.X + dx, wall.Y + dy))) continue;
                var height = doc.RoomHeight(level, room);
                best = best is null ? height : Math.Max(best.Value, height);
                break;
            }
        }
        return best;
    }

    // Deux murs qui ne se touchent qu'en diagonale laissent, à chaque niveau, une arête vide (tier 0) : pas une
    // fuite, mais une pénalité récurrente que le joueur ne voit pas forcément en 2D.
    private static void DetectDiagonalGaps(int level, Dictionary<(int X, int Y), WallCell> walls, List<HashSet<(int X, int Y)>> footprints, BuildContext ctx)
    {
        var reported = 0;
        foreach (var (cell, _) in walls)
        {
            foreach (var (dx, dy) in new[] { (1, 1), (1, -1) })
            {
                var diagonal = (cell.X + dx, cell.Y + dy);
                if (!walls.ContainsKey(diagonal)) continue;
                var a = (cell.X + dx, cell.Y);
                var b = (cell.X, cell.Y + dy);
                if (walls.ContainsKey(a) || walls.ContainsKey(b)) continue;

                var aInside = footprints.Any(f => f.Contains(a));
                var bInside = footprints.Any(f => f.Contains(b));
                if (aInside == bInside) continue;

                var outside = aInside ? b : a;
                ctx.Issues.Add(PlanIssue.Warning("DiagonalGap", [], new GridPoint { X = outside.Item1, Y = outside.Item2 }, level: level));
                if (++reported >= 20) return;
            }
        }
    }
}
