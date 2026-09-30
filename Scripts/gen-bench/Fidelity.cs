using System.Text.Json;
using System.Text.Json.Nodes;
using ecocraft.BuildingPlanner;
using ecocraft.BuildingPlanner.Model;

// Fidélité de la migration v3 → v4 : baseline.json (ancien moteur, 72edacd) liste pour chaque plan v3 figé ses matériaux,
// ses pièces, ses objets posés et son housing ; chaque entrée est relue par PlanDocumentJson.Parse (matérialisation des
// sols et plafonds implicites), analysée par le nouveau moteur et comparée. Les entrées sont dans inputs/ à côté de
// baseline.json. Plans en mode architecture : l'ancien moteur s'arrêtait avant les pièces, seuls les matériaux comptent.
public static class Fidelity
{
    private const double Tolerance = 1e-4;   // floats écrits à la précision float, tiers déjà arrondis à 2 décimales

    // baselinePath : argument de la ligne de commande ou variable PLANNER_BASELINE ; absent → fidélité non testée.
    public static void Run(string? baselinePath, Catalog genBench, Action<string, string, bool, string> check)
    {
        if (baselinePath is null || !File.Exists(baselinePath)) { Console.WriteLine($"       info: ligne de base {baselinePath ?? "(PLANNER_BASELINE non défini)"} absente, fidélité non testée"); return; }
        var inputs = Path.Combine(Path.GetDirectoryName(baselinePath)!, "inputs");
        var engineTest = EngineTestCatalog();
        var plans = JsonNode.Parse(File.ReadAllText(baselinePath))!["plans"]!.AsArray();
        var counts = new Dictionary<string, int>();
        foreach (var plan in plans)
        {
            var source = plan!["source"]!.GetValue<string>();
            var json = File.ReadAllText(Path.Combine(inputs, source));
            var oldOps = JsonNode.Parse(json)!["architecture"]?["ops"]?.AsArray().Count ?? 0;
            var doc = PlanDocumentJson.Parse(json);
            var result = PlanAnalyzer.Analyze(doc, plan["catalog"]!.GetValue<string>() == "engine-test" ? engineTest : genBench);
            var diffs = new List<string>();

            var added = doc.Architecture.Ops.Count - oldOps;
            if (doc.SchemaVersion != 4) diffs.Add($"schemaVersion {doc.SchemaVersion}");
            if (added > 0 && !doc.Architecture.Ops.Take(added).Select((op, i) => op.Id == $"h{i + 1}").All(b => b)) diffs.Add("ops matérialisées pas en tête (h1..)");
            if (doc.Architecture.Ops.Any(op => op.Kind == "cells" && op.Cells!.Length / 3 > PlanValidator.MaxCellsPerOp)) diffs.Add("op > 2000 cellules");
            if (result.Issues.Any(i => i.Code == "TooManyOps")) diffs.Add("TooManyOps");

            var wasBlocked = plan["blocked"]!.GetValue<bool>();
            if (wasBlocked || result.Blocked)
            {
                // Bloqué avant : le nouveau validateur peut le laisser passer si l'erreur était un code retiré (signalé, pas compté).
                if (wasBlocked != result.Blocked) Console.WriteLine($"       note: {source} {(wasBlocked ? "n'est plus bloqué" : "devient bloqué : " + string.Join(", ", result.Issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Code)))}");
                if (!wasBlocked) diffs.Add("bloqué");
                Report(check, source, diffs, wasBlocked ? "bloqué avant" : "", counts);
                continue;
            }

            var materials = result.Materials.GroupBy(m => m.Material).ToDictionary(g => g.Key, g => g.Sum(m => m.Count), StringComparer.Ordinal);
            var expected = plan["materials"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<int>(), StringComparer.Ordinal);
            foreach (var name in materials.Keys.Union(expected.Keys).Order(StringComparer.Ordinal))
                if (materials.GetValueOrDefault(name) != expected.GetValueOrDefault(name)) diffs.Add($"{name} {expected.GetValueOrDefault(name)} → {materials.GetValueOrDefault(name)}");

            var architecture = plan["mode"]!.GetValue<string>() == PlanMode.Architecture;
            if (!architecture)
            {
                var rooms = plan["rooms"]!.AsArray();
                if (rooms.Count != result.Rooms.Count) diffs.Add($"{rooms.Count} pièce(s) → {result.Rooms.Count}");
                else
                    for (var i = 0; i < rooms.Count; i++)
                    {
                        var e = rooms[i]!;
                        var r = result.Rooms[i];
                        var want = $"{e["name"]} {e["contained"]} {e["failCode"]} vol {e["volume"]} murs {e["wallCount"]} arêtes {e["emptyEdgeCount"]}";
                        var got = $"{r.Name} {r.Contained.ToString().ToLowerInvariant()} {r.FailCode} vol {r.Volume} murs {r.WallCount} arêtes {r.EmptyEdgeCount}";
                        if (want != got) diffs.Add($"pièce {i} : {want} → {got}");
                        if (Math.Abs(e["averageTier"]!.GetValue<double>() - r.AverageTier) > Tolerance) diffs.Add($"pièce {i} tier {e["averageTier"]} → {r.AverageTier}");
                    }
                var placed = result.Objects.Count(o => o.Placed);
                if (placed != plan["objectsPlaced"]!.GetValue<int>()) diffs.Add($"objets posés {plan["objectsPlaced"]} → {placed}");
                var housing = plan["housing"]?.GetValue<double>();
                if (housing is null != result.Housing is null || housing is { } h && Math.Abs(h - result.Housing!.Total) > Tolerance) diffs.Add($"housing {housing} → {result.Housing?.Total}");
            }
            Report(check, source, diffs, $"{(architecture ? "architecture, matériaux seuls, " : "")}{added} op(s) matérialisée(s)", counts);
        }
        Console.WriteLine($"       info: fidélité {plans.Count} plans : " + string.Join(", ", counts.Select(c => $"{c.Value} {c.Key}")));
    }

    private static void Report(Action<string, string, bool, string> check, string source, List<string> diffs, string info, Dictionary<string, int> counts)
    {
        check("fidelity", source, diffs.Count == 0, diffs.Count == 0 ? info : string.Join(" ; ", diffs));
        var key = diffs.Count == 0 ? "identiques" : "différents";
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    // Catalogue de l'engine-test (MakeCatalog, sans bump de module) + BlastFurnaceItem et FooLampItem, copié de la ligne de base.
    private static Catalog EngineTestCatalog() => new()
    {
        Materials = new Dictionary<string, BlockMaterialInfo>
        {
            ["MortaredStoneItem"] = new() { Name = "MortaredStoneItem", Tier = 2 },
            ["HewnLogItem"] = new() { Name = "HewnLogItem", Tier = 2 },
            ["BrickItem"] = new() { Name = "BrickItem", Tier = 3 },
            ["LumberItem"] = new() { Name = "LumberItem", Tier = 3 },
            ["DirtItem"] = new() { Name = "DirtItem", Tier = 0 },
            ["CottonCurtainsItem"] = new() { Name = "CottonCurtainsItem", Tier = 5, IgnoreRooms = true },
        },
        Objects = new Dictionary<string, WorldObjectInfo>
        {
            ["AnvilItem"] = new() { Name = "AnvilItem", Cells = Box(2, 1, 2), AttachedSide = "Down", IsCraftingTable = true, Requirements = new() { MaterialTier = 1.8f, Volume = 18, RequiresContainment = true }, Housing = new() { Category = "Industrial", BaseValue = 0 } },
            ["BloomeryItem"] = new() { Name = "BloomeryItem", Cells = Box(2, 1, 2), AttachedSide = "Down", IsCraftingTable = true, Requirements = new() { MaterialTier = 1.8f, Volume = 28, RequiresContainment = true } },
            ["HewnDoorItem"] = new() { Name = "HewnDoorItem", Tier = 2, AttachedSide = "Down", MustBeGridAligned = true, Cells =
            [
                new(new Vec3i(0, 0, 0), OccupancyKind.Wall), new(new Vec3i(0, 1, 0), OccupancyKind.Wall),
                new(new Vec3i(0, 0, -1), OccupancyKind.Occupied), new(new Vec3i(0, 0, 1), OccupancyKind.Occupied),
                new(new Vec3i(0, 1, -1), OccupancyKind.Occupied), new(new Vec3i(0, 1, 1), OccupancyKind.Occupied),
            ] },
            ["CastIronBedItem"] = new() { Name = "CastIronBedItem", Cells = Box(2, 2, 3), AttachedSide = "Down", Housing = new() { Category = "Bedroom", BaseValue = 7.5f, TypeForRoomLimit = "Bed", DiminishingReturnMultiplier = 0.3f } },
            ["BathtubItem"] = new() { Name = "BathtubItem", Cells = Box(1, 1, 2), AttachedSide = "Down", Housing = new() { Category = "Bathroom", BaseValue = 5f, TypeForRoomLimit = "Bath", DiminishingReturnMultiplier = 0.5f } },
            ["WoodenChairItem"] = new() { Name = "WoodenChairItem", Cells = Box(1, 1, 1), AttachedSide = "Down", Housing = new() { Category = "Seating", BaseValue = 1.5f, TypeForRoomLimit = "Chair", DiminishingReturnMultiplier = 0.5f } },
            ["WoodenTableItem"] = new() { Name = "WoodenTableItem", Cells = Box(1, 1, 1), AttachedSide = "Down", HasTableSurface = true, Housing = new() { Category = "Living Room", BaseValue = 2f, TypeForRoomLimit = "Table", DiminishingReturnMultiplier = 0.5f } },
            ["TikiTorchItem"] = new() { Name = "TikiTorchItem", Cells = Box(1, 3, 1), AttachedSide = "Down", Housing = new() { Category = "Outdoor", BaseValue = 2f, TypeForRoomLimit = "Lights", DiminishingReturnMultiplier = 0.4f } },
            ["BenchItem"] = new() { Name = "BenchItem", Cells = Box(2, 1, 1), AttachedSide = "Down", Requirements = new() { Volume = 6 }, Housing = new() { Category = "Seating", BaseValue = 3f, TypeForRoomLimit = "Chair", DiminishingReturnMultiplier = 0.5f } },
            ["CandleStandItem"] = new() { Name = "CandleStandItem", Cells = Box(1, 1, 1), AttachedSide = "Down", CanBeOnSurface = true, Housing = new() { Category = "Lighting", BaseValue = 1f, TypeForRoomLimit = "Lights", DiminishingReturnMultiplier = 0.5f } },
            ["BlastFurnaceItem"] = new() { Name = "BlastFurnaceItem", Cells = Box(3, 5, 3), AttachedSide = "Down", IsCraftingTable = true, Requirements = new() { MaterialTier = 3f, Volume = 75, RequiresContainment = true } },
            ["FooLampItem"] = new() { Name = "FooLampItem", Cells = Box(1, 1, 1), AttachedSide = "Down", Housing = new() { Category = "FooCategory", BaseValue = 1f, TypeForRoomLimit = "Lamp" } },
        },
        Build = BuildRules.Vanilla(),
        Housing = new HousingRules
        {
            Categories = HousingRules.Vanilla().Categories,
            RoomTiers = HousingRules.Vanilla().RoomTiers,
            OccupancyMultipliers = [1f, 1f, 0.6f, 0.38333333f, 0.25f],
        },
    };

    private static IReadOnlyList<OccupancyCell> Box(int sx, int sy, int sz)
    {
        var cells = new List<OccupancyCell>();
        for (var x = 0; x < sx; x++) for (var y = 0; y < sy; y++) for (var z = 0; z < sz; z++) cells.Add(new OccupancyCell(new Vec3i(x, y, z), OccupancyKind.Occupied));
        return cells;
    }
}
