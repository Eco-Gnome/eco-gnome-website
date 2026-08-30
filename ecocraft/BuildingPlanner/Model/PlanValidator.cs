namespace ecocraft.BuildingPlanner.Model;

// Validation d'entrée (bloquante) du document : bornes, clés, identifiants, références. Les matériaux ou
// types inconnus ne sont pas rejetés ici : l'analyse les signale et dégrade (tier 0 / objet ignoré).
public static class PlanValidator
{
    public const int MaxGridSide = 200;
    public const int MaxObjects = 2000;
    public const int MaxRooms = 200;
    public const int MaxLevels = 10;
    public const int MaxHeight = 50;
    public const int MaxDocumentBytes = 256 * 1024;

    public static List<PlanIssue> Validate(PlanDocument doc)
    {
        var issues = new List<PlanIssue>();

        if (doc.Grid.Width < 1 || doc.Grid.Depth < 1 || doc.Grid.Width > MaxGridSide || doc.Grid.Depth > MaxGridSide)
            issues.Add(PlanIssue.Error("GridSizeInvalid", [MaxGridSide.ToString()]));
        if (doc.Levels.Count == 0 || doc.Levels.Count > MaxLevels) { issues.Add(PlanIssue.Error("TooManyLevels", [MaxLevels.ToString()])); return issues; }
        if (doc.Levels.Sum(l => l.Objects.Count) > MaxObjects) issues.Add(PlanIssue.Error("TooManyObjects", [MaxObjects.ToString()]));
        if (doc.Levels.Sum(l => l.Rooms.Count) > MaxRooms) issues.Add(PlanIssue.Error("TooManyRooms", [MaxRooms.ToString()]));
        if (doc.Defaults.WallHeight < 1 || doc.Defaults.WallHeight > MaxHeight) issues.Add(PlanIssue.Error("InvalidHeight", ["defaults.wallHeight"]));

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var objectIds = new HashSet<string>(StringComparer.Ordinal);
        for (var k = 0; k < doc.Levels.Count; k++)
        {
            var level = doc.Levels[k];
            if (level.Height is < 1 or > MaxHeight) { issues.Add(PlanIssue.Error("InvalidHeight", [$"levels[{k}]"], level: k)); continue; }

            // Sommet du niveau, surcharges comprises : la grille voxel doit rester bornée.
            var top = doc.LevelHeight(k);
            foreach (var (key, wall) in level.Walls)
            {
                if (!PlanKeys.TryParse(key, out var x, out var y)) { issues.Add(PlanIssue.Error("InvalidCellKey", [key], level: k)); continue; }
                if (!InGrid(doc, x, y)) issues.Add(PlanIssue.Error("CellOutOfGrid", [key], level: k));
                if (wall.Height is < 1 or > MaxHeight) issues.Add(PlanIssue.Error("InvalidHeight", [key], level: k));
                else if (wall.Height is { } wh) top = Math.Max(top, wh);
                if (string.IsNullOrWhiteSpace(wall.Material)) issues.Add(PlanIssue.Error("MissingWallMaterial", [key], level: k));
            }

            foreach (var key in level.Floors.Keys.Concat(level.Holes.Keys))
            {
                if (!PlanKeys.TryParse(key, out var x, out var y)) { issues.Add(PlanIssue.Error("InvalidCellKey", [key], level: k)); continue; }
                if (!InGrid(doc, x, y)) issues.Add(PlanIssue.Error("CellOutOfGrid", [key], level: k));
            }

            foreach (var room in level.Rooms)
            {
                if (string.IsNullOrWhiteSpace(room.Id) || !ids.Add(room.Id)) issues.Add(PlanIssue.Error("DuplicateId", [room.Id], roomId: room.Id, level: k));
                if (room.Height is < 1 or > MaxHeight) issues.Add(PlanIssue.Error("InvalidHeight", [room.Name], roomId: room.Id, level: k));
                else if (room.Height is { } rh) top = Math.Max(top, rh);
                if (!InGrid(doc, room.Seed.X, room.Seed.Y)) issues.Add(PlanIssue.Error("SeedOutOfGrid", [room.Name], roomId: room.Id, level: k));
            }

            foreach (var obj in level.Objects)
            {
                if (string.IsNullOrWhiteSpace(obj.Id) || !objectIds.Add(obj.Id)) issues.Add(PlanIssue.Error("DuplicateId", [obj.Id], objectId: obj.Id, level: k));
                if (obj.Rotation is < 0 or > 3) issues.Add(PlanIssue.Error("InvalidRotation", [obj.Id], objectId: obj.Id, level: k));
                if (string.IsNullOrWhiteSpace(obj.Type)) issues.Add(PlanIssue.Error("MissingObjectType", [obj.Id], objectId: obj.Id, level: k));
            }

            if (doc.LevelBaseY(k) + top + 1 > MaxHeight) issues.Add(PlanIssue.Error("BuildingTooHigh", [MaxHeight.ToString()], level: k));
        }

        var byId = doc.AllObjects().Select(e => e.Object).Where(o => !string.IsNullOrWhiteSpace(o.Id)).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var obj in byId.Values.Where(o => o.AttachedTo is not null))
        {
            if (!byId.ContainsKey(obj.AttachedTo!)) { issues.Add(PlanIssue.Error("AttachTargetMissing", [obj.Id], objectId: obj.Id)); continue; }

            // Cycle : on remonte la chaîne des parents, bornée par le nombre d'objets.
            var seen = new HashSet<string> { obj.Id };
            var current = obj;
            var safe = true;
            while (current.AttachedTo is not null && byId.TryGetValue(current.AttachedTo, out var parent))
            {
                if (!seen.Add(parent.Id)) { safe = false; break; }
                current = parent;
            }
            if (!safe) issues.Add(PlanIssue.Error("AttachCycle", [obj.Id], objectId: obj.Id));
        }

        return issues;
    }

    public static bool InGrid(PlanDocument doc, int x, int y) => x >= 0 && y >= 0 && x < doc.Grid.Width && y < doc.Grid.Depth;
}
