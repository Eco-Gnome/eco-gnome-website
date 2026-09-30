using System.Text.Json;
using ecocraft.BuildingPlanner;
using ecocraft.BuildingPlanner.Generator;
using ecocraft.BuildingPlanner.Model;

// Bench du générateur (bloc 1) : 7 fixtures, assertions 1-7 du plan, PlanDocument écrits dans out/ ; puis fidélité de la
// migration v3 → v4 (Fidelity.cs) contre la ligne de base de l'ancien moteur.
var root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "GenBench.csproj"))) root = Path.GetDirectoryName(root)!;
var fixtures = Path.Combine(root, "fixtures");
var outDir = Path.Combine(root, "out");
Directory.CreateDirectory(outDir);

string[] materials = ["MortaredSandstoneItem", "HewnLogItem", "LumberItem", "BrickItem", "MortaredStoneItem", "FarEastLumberItem", "ReinforcedConcreteItem", "CorrugatedSteelItem",
    "AshlarLimestoneItem", "CompositeOakLumberItem", "FlatSteelItem", "FramedGlassItem", "AdobeItem"];
var catalog = new Catalog
{
    Materials = materials.ToDictionary(m => m, m => new BlockMaterialInfo { Name = m, Tier = m is "HewnLogItem" or "AdobeItem" ? 1 : m == "MortaredSandstoneItem" || m == "MortaredStoneItem" ? 2 : m is "ReinforcedConcreteItem" or "CorrugatedSteelItem" ? 4 : m is "AshlarLimestoneItem" or "CompositeOakLumberItem" or "FlatSteelItem" or "FramedGlassItem" ? 5 : 3 }, StringComparer.Ordinal),
    // Porte 1×1×2 : deux cellules « Wall » (comme HewnDoorItem sans ses cellules d'occupation de part et d'autre).
    Objects = new Dictionary<string, WorldObjectInfo>(StringComparer.Ordinal)
    {
        ["HewnDoorItem"] = new() { Name = "HewnDoorItem", Tier = 1, AttachedSide = "Down", MustBeGridAligned = true, Cells = [new(new Vec3i(0, 0, 0), OccupancyKind.Wall), new(new Vec3i(0, 1, 0), OccupancyKind.Wall)] },
        // Shoji 4×2 et grande porte 5×4 dans le plan y-z au rot 0 (WorldObjectOccupancyAutoGen).
        ["ShojiDoorItem"] = new() { Name = "ShojiDoorItem", Tier = 2, AttachedSide = "Down", MustBeGridAligned = true, Cells = [.. Enumerable.Range(0, 4).SelectMany(x => new[] { new OccupancyCell(new Vec3i(x, 0, 0), OccupancyKind.Wall), new OccupancyCell(new Vec3i(x, 1, 0), OccupancyKind.Wall) })] },
        ["LargeLumberDoorItem"] = new() { Name = "LargeLumberDoorItem", Tier = 3, AttachedSide = "Down", MustBeGridAligned = true, Cells = [.. Enumerable.Range(0, 5).SelectMany(z => Enumerable.Range(0, 4).Select(y => new OccupancyCell(new Vec3i(0, y, z), OccupancyKind.Wall)))] },
        // Mobilier hewn (occupancy du serveur, WorldObjectOccupancyAutoGen) : table 2×1 vers −X, chaise 1×1×2, banc 2×2, dresser 2×1×2, chevet 1×1×2.
        ["HewnTableItem"] = Piece("HewnTableItem", [(-1, 0, 0), (0, 0, 0)]),
        ["HewnChairItem"] = Piece("HewnChairItem", [(0, 0, 0), (0, 1, 0)]),
        ["HewnBenchItem"] = Piece("HewnBenchItem", [(-1, 0, 0), (-1, 0, 1), (0, 0, 0), (0, 0, 1)]),
        ["HewnDresserItem"] = Piece("HewnDresserItem", [(0, 0, 0), (0, 1, 0), (1, 0, 0), (1, 1, 0)]),
        ["HewnNightstandItem"] = Piece("HewnNightstandItem", [(0, 0, 0), (0, 1, 0)]),
    },
    Build = CatalogJson.ParseBuildRules(null),
    Housing = CatalogJson.ParseHousingRules(null),
};
const string doorType = "HewnDoorItem";
var doorInfos = catalog.Objects.Values.Where(o => o.HasWallCells).Select(o => new FurnitureInfo(o.Name, o.PlacedCells.Select(c => c.Offset).ToList())).ToList();
var furniture = catalog.Objects.Values.Where(o => !o.HasWallCells).Select(o => new FurnitureInfo(o.Name, o.PlacedCells.Select(c => c.Offset).ToList())).ToList();

var failures = 0;
void Check(string fixture, string name, bool ok, string detail = "")
{
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} [{fixture}] {name}{(detail.Length > 0 ? " — " + detail : "")}");
    if (!ok) failures++;
}

// 7. Schéma : lève sur liste vide, enum de matériaux présente.
Console.WriteLine("schema");
try { BuildingProgramSchema.Build([]); Check("schema", "Build(vide) lève", false); }
catch (ArgumentException) { Check("schema", "Build(vide) lève", true); }
var schema = BuildingProgramSchema.Build(materials, doorInfos, furniture);
var schemaJson = schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(outDir, "schema.json"), schemaJson);
Check("schema", "matériaux listés une fois, portes et meubles + emprises", schema["description"]!.GetValue<string>().Contains("MortaredSandstoneItem") && schemaJson.Contains("\"additionalProperties\": false") && schemaJson.Contains("ShojiDoorItem 4") && schemaJson.Contains("HewnBenchItem 2"), $"{schemaJson.Length} chars");
Check("schema", "programme ↔ schéma", SchemaMatchesDto(schema), "propriétés du schéma = propriétés du DTO");

foreach (var file in Directory.GetFiles(fixtures, "*.json").OrderBy(f => f, StringComparer.Ordinal))
{
    var fixture = Path.GetFileNameWithoutExtension(file);
    Console.WriteLine(fixture);
    var json = File.ReadAllText(file);
    var program = BuildingProgramJson.Parse(json);
    var result = BuildingGenerator.Generate(program, materials, doorInfos, furniture);
    foreach (var note in result.Notes) Console.WriteLine($"       note: {note}");

    if (fixture == "empty")
    {
        Check(fixture, "programme vide → Error EmptyProgram", result.Document is null && result.Error == "EmptyProgram", result.Error ?? "");
        continue;
    }

    // 1. Validation sans Error.
    Check(fixture, "1 Generate → Document", result.Document is not null, result.Error ?? "");
    if (result.Document is null) continue;
    var doc = result.Document;
    var issues = PlanValidator.Validate(doc);
    Check(fixture, "1 PlanValidator sans Error", issues.All(i => i.Severity != IssueSeverity.Error), string.Join(", ", issues.Select(i => i.Code)));

    // 2. Budgets.
    var docJson = PlanDocumentJson.Serialize(doc);
    File.WriteAllText(Path.Combine(outDir, fixture + ".json"), docJson);
    var work = doc.Architecture.Ops.Sum(op => ArchShapes.WorkVolume(op, doc.Grid.Width, doc.Grid.Depth, doc.Architecture.Height));
    Check(fixture, "2 budgets", doc.Architecture.Ops.Count <= 460 && work <= 40_000_000 && docJson.Length <= 256 * 1024,
        $"{doc.Architecture.Ops.Count} ops, work {work}, {docJson.Length} o, grille {doc.Grid.Width}×{doc.Grid.Depth}, hauteur {doc.Architecture.Height}, niveaux {string.Join("/", doc.Levels.Select((l, k) => $"{doc.LevelBaseY(k)}+{l.Height}"))}");

    // 3. Matériaux et formes.
    var additive = doc.Architecture.Ops.Where(op => !op.Subtract).ToList();
    Check(fixture, "3 matériaux autorisés", additive.All(op => materials.Contains(op.Material)), string.Join(", ", additive.Select(op => op.Material).Distinct()));
    Check(fixture, "3 formes connues", additive.All(op => op.Form is not null && BuildingGenerator.Forms.Contains(op.Form) && op.Rot is >= 0 and <= 3),
        string.Join(", ", additive.GroupBy(op => op.Form).Select(g => $"{g.Key}×{g.Count()}")));
    // Formes du matériau : FarEast n'a ni Wall, ni Stairs, ni WindowGrilles ; Wall_NN n'existe qu'en FarEast. Murs et fenêtres
    // FarEast posés tournés : une boîte le long de x en rot 0, le long de y en rot 1.
    var farEast = additive.Where(op => op.Material == "FarEastLumberItem").ToList();
    Check(fixture, "3 formes du matériau", farEast.All(op => op.Form is not ("Wall" or "Stairs" or "WindowGrilles"))
        && additive.Except(farEast).All(op => op.Form?.StartsWith("Wall_") != true)
        && farEast.Where(op => op.Kind == "box" && (op.Form!.StartsWith("Wall_") || op.Form == "Window")).All(op => op.A[0] != op.B[0] ? op.Rot == 0 : op.A[1] == op.B[1] || op.Rot == 1),
        $"{farEast.Count} op(s) FarEast");
    // Adobe : murs WallMid tournés comme FarEast (ni Wall, ni Stairs, ni WindowGrilles) ; WallMid n'existe qu'en adobe.
    var adobe = additive.Where(op => op.Material == "AdobeItem").ToList();
    Check(fixture, "3 formes adobe", adobe.All(op => op.Form is not ("Wall" or "Stairs" or "WindowGrilles"))
        && additive.Except(adobe).All(op => op.Form != "WallMid")
        && adobe.Where(op => op.Kind == "box" && op.Form is "WallMid" or "Window").All(op => op.A[0] != op.B[0] ? op.Rot == 0 : op.A[1] == op.B[1] || op.Rot == 1),
        $"{adobe.Count} op(s) adobe");
    if (fixture == "adobe-pueblo")
    {
        int Count(string form) => adobe.Count(op => op.Form == form);
        // Angles : 2 anneaux de murs + le muret du toit plat ; toit plat : parapets RoofMid / RoofCorner et dalle RoofFill.
        Check(fixture, "3 adobe : 12 angles, 4 T, 1 X, toit plat", Count("WallCorner") == 12 && Count("WallT") == 4 && Count("WallX") == 1 && Count("RoofCorner") == 4 && Count("RoofMid") == 4 && Count("RoofFill") == 1 && Count("Cube") == 0,
            $"WallCorner×{Count("WallCorner")}, WallT×{Count("WallT")}, WallX×{Count("WallX")}, RoofCorner×{Count("RoofCorner")}, RoofMid×{Count("RoofMid")}, RoofFill×{Count("RoofFill")}, Cube×{Count("Cube")}");
    }
    // Acier et béton : pas de WindowGrilles au marteau, leurs fenêtres sont des Window.
    var t4 = additive.Where(op => op.Material is "ReinforcedConcreteItem" or "CorrugatedSteelItem").ToList();
    Check(fixture, "3 fenêtres T4 = Window", t4.All(op => op.Form != "WindowGrilles"), $"{t4.Count(op => op.Form == "Window")} fenêtre(s) Window T4");
    // T5 : fenêtres Window pour la pierre de taille et l'acier plat, escaliers FloatStairs pour l'acier plat (pas de Stairs).
    var t5 = additive.Where(op => op.Material is "AshlarLimestoneItem" or "FlatSteelItem").ToList();
    Check(fixture, "3 formes T5", t5.All(op => op.Form != "WindowGrilles") && t5.All(op => op.Material != "FlatSteelItem" || op.Form != "Stairs"),
        $"{t5.Count(op => op.Form == "Window")} fenêtre(s) Window, {t5.Count(op => op.Form == "FloatStairs")} FloatStairs");
    // Toits : aucun bloc de toit dans l'anneau de murs d'un volume (un toit complet sous un étage en retrait l'entaillerait) ;
    // FarEast : première course en rives relevées ; pagode : trois auvents sans faîte, 4 angles par course.
    var roofOps = additive.Where(op => op.Form!.StartsWith("Roof", StringComparison.Ordinal)).ToList();
    var roofCells = new HashSet<(int X, int Y, int Z)>();
    foreach (var op in roofOps) ArchShapes.Paint(op, doc.Grid.Width, doc.Grid.Depth, doc.Architecture.Height, (x, y, z) => roofCells.Add((x, y, z)));
    bool InWallRing(VolumeElement v, (int X, int Y, int Z) c) => c.Z > v.BaseZ && c.Z <= v.BaseZ + v.Height
        && ((c.X == v.X || c.X == v.X + v.Width - 1) && c.Y >= v.Y && c.Y < v.Y + v.Depth || (c.Y == v.Y || c.Y == v.Y + v.Depth - 1) && c.X >= v.X && c.X < v.X + v.Width);
    var roofInWalls = program.Volumes.Take(40).Sum(v => roofCells.Count(c => InWallRing(v, c)));
    Check(fixture, "3 toits hors des murs", roofInWalls == 0, $"{roofInWalls} cellule(s) de toit dans un mur");
    var farEastRoofs = program.Roofs.Where(r => r.Material == "FarEastLumberItem" && r.Style is "hip" or "gable" or "shed").ToList();
    Check(fixture, "3 rives FarEast", farEastRoofs.Count == 0 || roofOps.Any(op => op.Form == "RoofEdgeSide"), $"{roofOps.Count(op => op.Form is "RoofEdgeSide" or "RoofEdgeCorner")} op(s) de rive");
    if (fixture == "fareast-pagoda")
    {
        int CornerCells(string form) => roofOps.Where(op => op.Form == form).Sum(op => op.Cells!.Length / 3);
        Check(fixture, "3 pagode : un faîte, angles à chaque course", roofOps.Count(op => op.Form == "RoofPeak") == 1 && CornerCells("RoofEdgeCorner") == 16 && CornerCells("RoofCorner") == 4 * (1 + 1 + 1 + 2),
            $"RoofPeak×{roofOps.Count(op => op.Form == "RoofPeak")}, cellules RoofEdgeCorner {CornerCells("RoofEdgeCorner")}, RoofCorner {CornerCells("RoofCorner")}");
    }
    Check(fixture, "3 ids g1..gN", doc.Architecture.Ops.Select((op, i) => op.Id == $"g{i + 1}").All(b => b));
    Check(fixture, "3 mode maison, aucune pièce", doc.Mode == PlanMode.House && doc.Levels.All(l => l.Rooms.Count == 0));
    var columnIssues = ColumnIssues(doc, requireCover: false);
    Check(fixture, "3 poteaux posés sur leur appui, sans remplacer un bloc", columnIssues.Count == 0, string.Join(" · ", columnIssues));

    // 4. Déterminisme.
    var again = BuildingGenerator.Generate(BuildingProgramJson.Parse(json), materials, doorInfos, furniture);
    Check(fixture, "4 déterminisme", again.Document is not null && PlanDocumentJson.Serialize(again.Document) == docJson);

    // 5. Clôture : le centre de chaque volume à z = baseZ+1 est dans une poche fermée (portes posées) ; une porte par embrasure.
    var built = GridBuilder.Build(doc, catalog);
    ObjectPlacer.PlaceAll(built);
    var pockets = PocketDetector.Detect(built);
    var volumeIndex = 0;
    foreach (var v in program.Volumes.Take(40))
    {
        volumeIndex++;
        var w = Math.Clamp(v.Width, 3, doc.Grid.Width);
        var d = Math.Clamp(v.Depth, 3, doc.Grid.Depth);
        var x0 = Math.Clamp(v.X, 0, doc.Grid.Width - w);
        var y0 = Math.Clamp(v.Y, 0, doc.Grid.Depth - d);
        var baseZ = Math.Clamp(v.BaseZ, 0, 300);
        var (cx, cy) = (x0 + w / 2, y0 + d / 2);
        if (built.IsWallVoxel(built.Grid.Get(Geometry.PlanToEco(cx, cy, baseZ + 1)))) { Console.WriteLine($"       info: volume {volumeIndex} centre ({cx},{cy}) occupé, clôture non testée"); continue; }
        var label = pockets.LabelAt(cx, cy, baseZ + 1);
        Check(fixture, $"5 clôture volume {volumeIndex} (z={baseZ + 1})", label > 0, label > 0 ? $"{pockets.Pockets[label - 1].Count} cellules" : "pas dans une poche fermée");
    }
    var doorCount = program.Openings.Count(o => o.Kind == "door" && (program.Volumes.Any(v => v.Id == o.TargetId) || program.Partitions.Any(p => p.Id == o.TargetId)));
    var doors = doc.Levels.SelectMany(l => l.Objects).Where(o => o.Id.StartsWith('d')).ToList();
    var doorless = result.Notes.Count(n => n.Contains("chevauche une autre porte") || n.Contains("embrasure vide"));   // embrasures laissées sans porte, notées
    Check(fixture, "5 une porte par embrasure", doors.Count == doorCount - doorless && doors.All(d => d.Type == doorType && d.Z >= 1 && d.Rotation is >= 0 and <= 3) && doors.Select((d, i) => d.Id == $"d{i + 1}").All(b => b),
        $"{doorCount} embrasure(s) : " + string.Join(", ", doors.Select(d => $"{d.Id}@{d.X},{d.Y},{d.Z} r{d.Rotation}")));
    // Mobilier : ids f1..fN, posé au sol (Z null), types du catalogue, jamais plus que demandé ; la fixture meublée en pose 6 sur 9.
    var pieces = doc.Levels.SelectMany(l => l.Objects).Where(o => o.Id.StartsWith('f')).ToList();
    Check(fixture, "5 mobilier", pieces.Count <= program.Furniture.Count && pieces.All(o => o.Z is null && o.Rotation is >= 0 and <= 3 && furniture.Any(f => f.Name == o.Type))
        && pieces.Select((o, i) => o.Id == $"f{i + 1}").All(b => b) && (fixture != "hewn-log-furnished" || pieces.Count == 6),
        string.Join(", ", pieces.Select(o => $"{o.Id} {o.Type}@{o.X},{o.Y} r{o.Rotation}")));
    var windowCount = program.Openings.Take(40).Count(o => o.Kind == "window" && (program.Volumes.Any(v => v.Id == o.TargetId) || program.Partitions.Any(p => p.Id == o.TargetId)));
    static bool IsWindow(string? form) => form is "WindowGrilles" or "Window";   // Window : fenêtre FarEast
    windowCount += program.Blocks.Take(BuildingGenerator.MaxBlocks).Count(b => IsWindow(b.Form));   // fenêtres posées en blocs (fût du phare)
    Check(fixture, "5 fenêtres non creusées", doc.Architecture.Ops.Count(op => op.Subtract) == doorCount + program.Stairs.Count(s => s.ToZ > s.FromZ && s.FromZ >= 0)
        && doc.Architecture.Ops.Count(op => IsWindow(op.Form)) == windowCount,
        $"{doc.Architecture.Ops.Count(op => IsWindow(op.Form))} fenêtres, {doc.Architecture.Ops.Count(op => op.Subtract)} soustractions");

    // 6. Bout en bout : analyse sans pièce, puis avec les pièces que reconcileRooms créerait (information).
    var analysis = PlanAnalyzer.Analyze(doc, catalog);
    Check(fixture, "6 Analyze non bloqué, matériaux > 0", !analysis.Blocked && analysis.Materials.Sum(m => m.Count) > 0,
        $"{string.Join(", ", analysis.Materials.Select(m => $"{m.Material} {m.Count}"))}; issues {string.Join(", ", analysis.Issues.Select(i => i.Code).Distinct())}");
    var seeded = PlanDocumentJson.Parse(docJson);
    SeedRooms(seeded, catalog);
    var roomAnalysis = PlanAnalyzer.Analyze(seeded, catalog);
    Console.WriteLine($"       info: {seeded.Levels.Sum(l => l.Rooms.Count)} pièce(s) auto : " + string.Join(" | ", roomAnalysis.Rooms.Select(r => $"{r.Name} {(r.Contained ? "contenue" : "ouverte:" + r.FailCode)} vol {r.Volume} tier {r.AverageTier}")));
    if (roomAnalysis.Blocked) Console.WriteLine("       info: analyse avec pièces bloquée : " + string.Join(", ", roomAnalysis.Issues.Select(i => i.Code)));
    Console.WriteLine("       info: objets : " + string.Join(", ", roomAnalysis.Objects.Select(o => $"{o.Id} {(o.Placed ? "posé" : "NON POSÉ")}")) + " ; issues " + string.Join(", ", roomAnalysis.Issues.Where(i => i.Severity != IssueSeverity.Info).Select(i => i.Code).Distinct()));
    if (fixture is not "adversarial-oob")
    {
        Check(fixture, "6 pièces contenues avec portes posées", roomAnalysis.Rooms.Count > 0 && roomAnalysis.Rooms.All(r => r.Contained && r.Volume > 0) && roomAnalysis.Objects.All(o => o.Placed),
            string.Join(" | ", roomAnalysis.Rooms.Select(r => $"{r.Name} {(r.Contained ? "contenue" : r.FailCode)} vol {r.Volume} housing {r.Housing?.Value}")));
        var noDoor = BuildingGenerator.Generate(BuildingProgramJson.Parse(json), materials).Document!;
        SeedRooms(noDoor, catalog);
        Console.WriteLine("       info: sans doorType : " + string.Join(" | ", PlanAnalyzer.Analyze(noDoor, catalog).Rooms.Select(r => $"{r.Name} {(r.Contained ? "contenue" : "ouverte:" + r.FailCode)}")));
    }
    // Sous un toit à deux ou quatre pans, la pièce monte jusqu'au toit (pas de grenier à part) et les marches sont remplies :
    // aucune arête vide. Exceptions : la lanterne du phare (dalle et blocs, pas un volume) et l'aile v4 du manoir (son toit
    // commence une rangée à l'intérieur de son emprise : plafond gardé, grenier à part).
    var edgeRooms = roomAnalysis.Rooms.Where(r => r.Contained && r.EmptyEdgeCount > 0).ToList();
    Check(fixture, "6 aucune arête vide", edgeRooms.Count == (fixture is "harbor-lighthouse" or "mortared-manor" ? 1 : 0),
        string.Join(" | ", edgeRooms.Select(r => $"{r.Name} {r.EmptyEdgeCount} arête(s) vide(s)")));
    var open = program.Volumes.Take(40).Where(v => OpenToRoof(v, program)).ToList();
    var attics = open.Where(v => pockets.Pockets.Any(p => p.MinZ >= v.BaseZ + v.Height + 1 && p.Seed.X >= v.X && p.Seed.X < v.X + v.Width && p.Seed.Y >= v.Y && p.Seed.Y < v.Y + v.Depth)).ToList();
    Check(fixture, "6 pas de grenier sous un toit à pans", attics.Count == 0,
        $"{open.Count} volume(s) ouvert(s) jusqu'au toit" + (attics.Count > 0 ? ", grenier au-dessus de " + string.Join(", ", attics.Select(v => v.Id)) : ""));
}

// Plafonds : a (pignon, débord 2, cloison pleine hauteur) et b (croupe sans débord, posée une couche au-dessus du plafond) sont
// ouverts jusqu'au toit, sans arête vide, la cloison monte jusqu'au toit ; c (flatCeiling) garde son plafond, sans remplissage,
// et son grenier est une poche à part.
Console.WriteLine("ceilings");
{
    VolumeElement House(string id, int x, string material, bool flat) => new() { Id = id, X = x, Y = 3, Width = 10, Depth = 8, Height = 3, WallMaterial = material, FloorMaterial = material, CeilingMaterial = material, FlatCeiling = flat };
    RoofElement Roof(int x, string style, int baseZ, int overhang, string material) => new() { X = x, Y = 3, Width = 10, Depth = 8, BaseZ = baseZ, Style = style, Overhang = overhang, Material = material };
    var program = new BuildingProgram
    {
        Grid = new ProgramGrid { Width = 42, Depth = 14 },
        Volumes = [House("a", 3, "HewnLogItem", false), House("b", 16, "BrickItem", false), House("c", 29, "HewnLogItem", true)],
        Roofs = [Roof(3, "gable", 4, 2, "HewnLogItem"), Roof(16, "hip", 5, 0, "LumberItem"), Roof(29, "gable", 4, 1, "HewnLogItem")],
        Partitions = [new PartitionElement { Id = "p", X0 = 7, Y0 = 4, X1 = 7, Y1 = 9, Height = 3, Material = "HewnLogItem" }],
    };
    var doc = BuildingGenerator.Generate(program, materials).Document!;
    bool Ceiling(int x) => doc.Architecture.Ops.Any(op => op.Form == "Floor" && op.A![2] == 4 && op.A[0] == x);
    Check("ceilings", "plafond seulement pour flatCeiling", !Ceiling(3) && !Ceiling(16) && Ceiling(29));
    var cubes = doc.Architecture.Ops.Where(op => op.Kind == "cells" && op.Form == "Cube").SelectMany(op => op.Cells!.Chunk(3)).ToList();
    Check("ceilings", "pas de remplissage sous le toit de c", !cubes.Any(c => c[0] > 29 && c[0] < 38 && c[1] > 3 && c[1] < 10), $"{cubes.Count} cellule(s) Cube");
    SeedRooms(doc, catalog);
    var seeds = doc.AllRooms().ToDictionary(e => e.Room.Id, e => e.Room.Seed.X);
    var rooms = PlanAnalyzer.Analyze(doc, catalog).Rooms;
    List<RoomAnalysis> In(int x0) => rooms.Where(r => seeds[r.RoomId] >= x0 && seeds[r.RoomId] < x0 + 10).ToList();
    static string Show(List<RoomAnalysis> rs) => string.Join(" + ", rs.Select(r => $"{(r.Contained ? "" : "ouverte ")}vol {r.Volume} ee {r.EmptyEdgeCount} tier {r.AverageTier}"));
    var (a, b, c) = (In(3), In(16), In(29));
    Check("ceilings", "a : deux pièces fermées de part et d'autre de la cloison, sans arête vide", a.Count == 2 && a.All(r => r.Contained && r.EmptyEdgeCount == 0), Show(a));
    Check("ceilings", "b : une pièce fermée sans arête vide", b.Count == 1 && b.All(r => r.Contained && r.EmptyEdgeCount == 0), Show(b));
    Check("ceilings", "c : pièce et grenier à part", c.Count == 2 && c.All(r => r.Contained), Show(c));
}

// Clôtures : pièce et rotation d'après les raccords du tracé (conventions mesurées), portillon, T, poteau-montant, familles,
// matériau sans clôture.
Console.WriteLine("fences");
{
    static FenceElement Fence(string material, bool closed, (int X, int Y)[] points, params (int X, int Y)[] gates) => new()
    {
        Material = material, Closed = closed, Points = points.Select(p => new FenceCell { X = p.X, Y = p.Y }).ToList(),
        Gates = gates.Select(g => new FenceCell { X = g.X, Y = g.Y }).ToList(),
    };
    var program = new BuildingProgram
    {
        Grid = new ProgramGrid { Width = 20, Depth = 20 },
        Columns = [new ColumnElement { X = 4, Y = 2, BaseZ = 1, Height = 3, Material = "FarEastLumberItem" }],   // montant sur le tracé
        Fences =
        [
            Fence("FarEastLumberItem", true, [(2, 2), (10, 2), (10, 8), (2, 8)], (6, 8)),
            Fence("FarEastLumberItem", false, [(6, 2), (6, 5)]),
            Fence("HewnLogItem", false, [(12, 2), (12, 6)]),
            Fence("CorrugatedSteelItem", false, [(14, 2), (18, 2)]),
            Fence("BrickItem", false, [(14, 5), (18, 5)]),
        ],
    };
    var result = BuildingGenerator.Generate(program, materials);
    var pieces = new Dictionary<(int, int, int), (string?, int?)>();
    foreach (var op in result.Document?.Architecture.Ops ?? [])
        for (var i = 0; op.Cells is not null && i < op.Cells.Length; i += 3) pieces[(op.Cells[i], op.Cells[i + 1], op.Cells[i + 2])] = (op.Form, op.Rot);
    string At(int x, int y) => pieces.TryGetValue((x, y, 1), out var p) ? $"{p.Item1}/{p.Item2}" : "—";
    (string Cell, string Want)[] expected =
    [
        ("2,2", "FenceCorner/2"), ("10,2", "FenceCorner/1"), ("10,8", "FenceCorner/0"), ("2,8", "FenceCorner/3"),
        ("4,2", "—"), ("3,2", "FenceMid/0"), ("5,2", "FenceMid/0"), ("2,5", "FenceMid/1"), ("6,8", "—"), ("5,8", "FenceEnd/2"), ("7,8", "FenceEnd/0"),
        ("6,2", "FenceT/2"), ("6,5", "FenceEnd/1"),
        ("12,2", "DocksFenceEndCap/0"), ("12,4", "DocksFenceMid/0"), ("12,6", "DocksFenceEndCap/2"),
        ("16,2", "Fence/0"), ("16,5", "—"),
    ];
    foreach (var (cell, want) in expected)
    {
        var xy = cell.Split(',').Select(int.Parse).ToArray();
        Check("fences", $"({cell}) {want}", At(xy[0], xy[1]) == want, At(xy[0], xy[1]));
    }
    Check("fences", "une couche (z = 1), matériau sans clôture noté", pieces.Keys.All(k => k.Item3 == 1) && result.Notes.Any(n => n.Contains("pas de clôture en BrickItem")), string.Join(" · ", result.Notes));
}

// Poteaux sous un auvent (véranda de Romain, 2026-09-30 : poteaux FarEast enfoncés dans le dallage, une couche vide sous le
// toit) : dallage à z 0, auvent FarEast à z 5. A = valeurs de l'IA (baseZ 0, height 4), B trop court, C décalé d'une couche
// et trop haut, D autre matériau : tous sur z 1..4, dallage intact, toit touché.
Console.WriteLine("columns");
{
    ColumnElement Row(int x, int y, int stepX, int stepY, int count, int baseZ, int height, string material) => new() { X = x, Y = y, StepX = stepX, StepY = stepY, Count = count, BaseZ = baseZ, Height = height, Material = material };
    var program = new BuildingProgram
    {
        Grid = new ProgramGrid { Width = 16, Depth = 16 },
        Volumes = [new VolumeElement { Id = "v1", X = 4, Y = 4, Width = 8, Depth = 8, Height = 4, WallMaterial = "FarEastLumberItem", FloorMaterial = "FarEastLumberItem", CeilingMaterial = "FarEastLumberItem" }],
        Slabs = [new SlabElement { X = 1, Y = 1, Width = 14, Depth = 14, Z = 0, Material = "MortaredStoneItem", Thickness = 1 }],
        Roofs = [new RoofElement { X = 4, Y = 4, Width = 8, Depth = 8, BaseZ = 5, Style = "hip", Overhang = 2, Material = "FarEastLumberItem" }],
        Columns = [Row(2, 2, 3, 0, 4, 0, 4, "FarEastLumberItem"), Row(2, 13, 3, 0, 4, 0, 3, "FarEastLumberItem"), Row(13, 5, 0, 3, 3, 1, 4, "HewnLogItem"), Row(2, 5, 0, 3, 3, 0, 4, "MortaredStoneItem")],
    };
    var doc = BuildingGenerator.Generate(program, materials).Document!;
    var columns = doc.Architecture.Ops.Where(op => op.Form == "Column").ToList();
    Check("columns", "14 poteaux sur z 1..4", columns.Count == 14 && columns.All(op => op.A![2] == 1 && op.B![2] == 4),
        string.Join(", ", columns.Select(op => $"{op.A![0]},{op.A[1]} z{op.A[2]}..{op.B![2]}")));
    var issues = ColumnIssues(doc, requireCover: true);
    Check("columns", "dallage intact, appui sous le pied, toit sur la tête", issues.Count == 0, string.Join(" · ", issues));
}

// Portes à leur vraie taille : une porte par embrasure, creusée à sa mesure, repli si elle ne tient pas, rien devant.
Console.WriteLine("doors");
{
    OpeningElement Door(string target, string side, int offset, string type) => new() { TargetId = target, Side = side, Offset = offset, Kind = "door", Width = 1, Height = 2, Door = type };
    var program = new BuildingProgram
    {
        Grid = new ProgramGrid { Width = 24, Depth = 16 },
        Volumes =
        [
            new VolumeElement { Id = "big", X = 1, Y = 1, Width = 12, Depth = 10, Height = 4, WallMaterial = "HewnLogItem", FloorMaterial = "HewnLogItem", CeilingMaterial = "HewnLogItem" },
            new VolumeElement { Id = "small", X = 15, Y = 1, Width = 4, Depth = 5, Height = 3, WallMaterial = "HewnLogItem", FloorMaterial = "HewnLogItem", CeilingMaterial = "HewnLogItem" },
        ],
        Openings = [Door("big", "south", 2, "ShojiDoorItem"), Door("big", "west", 2, "LargeLumberDoorItem"), Door("small", "south", 1, "ShojiDoorItem")],
        Furniture = [new FurnitureElement { Type = "HewnChairItem", X = 4, Y = 9, BaseZ = 0 }],   // juste devant la Shoji
    };
    var result = BuildingGenerator.Generate(program, materials, doorInfos, furniture);
    var doc = result.Document!;
    var objects = doc.Levels.SelectMany(l => l.Objects).ToList();
    var doors = objects.Where(o => o.Id.StartsWith('d')).ToList();
    var cuts = doc.Architecture.Ops.Where(o => o.Subtract).Select(o => $"{o.A![0]},{o.A[1]}..{o.B![0]},{o.B[1]} h{o.B[2] - o.A[2] + 1}").ToList();
    Check("doors", "une porte par embrasure : Shoji, grande porte, repli Hewn", doors.Select(d => d.Type).SequenceEqual(["ShojiDoorItem", "LargeLumberDoorItem", "HewnDoorItem"]),
        string.Join(", ", doors.Select(d => $"{d.Type}@{d.X},{d.Y} r{d.Rotation}")));
    Check("doors", "embrasures à la taille des portes (4×2, 5×4, 1×2)", cuts.SequenceEqual(["3,10..6,10 h2", "1,3..1,7 h4", "16,5..16,5 h2"]), string.Join(" | ", cuts));
    Check("doors", "meuble devant la porte refusé", objects.All(o => o.Type != "HewnChairItem") && result.Notes.Any(n => n.Contains("devant une porte")), string.Join(" · ", result.Notes));
    var analysis = PlanAnalyzer.Analyze(doc, catalog);
    var doorIssues = analysis.Issues.Where(i => i.ObjectId is not null && doors.Any(d => d.Id == i.ObjectId) && i.Severity != IssueSeverity.Info).Select(i => i.Code).ToList();
    Check("doors", "portes posées sans erreur par l'analyse", doorIssues.Count == 0 && analysis.Objects.Where(o => doors.Any(d => d.Id == o.Id)).All(o => o.Placed),
        string.Join(", ", doorIssues) + " ; " + string.Join(", ", analysis.Objects.Select(o => $"{o.Id} {(o.Placed ? "posé" : "NON POSÉ")}")));
}

// Description d'un plan pour l'IA : niveaux, ops, objets, pièces, vues de dessus ; ops cells résumées ; grands plans sans vues.
Console.WriteLine("plan-describe");
var cabin = BuildingGenerator.Generate(BuildingProgramJson.Parse(File.ReadAllText(Path.Combine(fixtures, "cabin.json"))), materials, doorInfos, furniture).Document!;
SeedRooms(cabin, catalog);
{
    var text = PlanDescriber.Describe(cabin, PlanAnalyzer.Analyze(cabin, catalog));
    File.WriteAllText(Path.Combine(outDir, "cabin.describe.txt"), text);
    Check("plan-describe", "cabane : niveau, mur, porte, pièce, vue de dessus", text.Contains("- level 0: base 0") && text.Contains("g1 box HewnLogItem Wall a 4,4,1 b 13,11,3 hollow thickness 1")
        && text.Contains("d1 HewnDoorItem level 0") && text.Contains(" closed, volume ") && text.Contains("Top views") && text.Contains("   4 ....AAAAAAAAAA"), $"{text.Length} chars");
    var pagoda = BuildingGenerator.Generate(BuildingProgramJson.Parse(File.ReadAllText(Path.Combine(fixtures, "fareast-pagoda.json"))), materials, doorInfos, furniture).Document!;
    var pagodaText = PlanDescriber.Describe(pagoda, null);
    File.WriteAllText(Path.Combine(outDir, "fareast-pagoda.describe.txt"), pagodaText);
    Check("plan-describe", "pagode : ops cells résumées par leur boîte", pagoda.Architecture.Ops.Any(o => o.Kind == "cells") && pagodaText.Contains(" cells ") && pagodaText.Contains(" within x "), $"{pagodaText.Length} chars");
    var big = PlanDocument.Empty(200, 200);
    big.Levels.Add(new PlanLevel());
    big.Architecture.Ops.Add(new ArchOp { Id = "a1", Kind = "box", Material = "HewnLogItem", A = [0, 0, 0], B = [199, 199, 0] });
    Check("plan-describe", "grand plan : vues de dessus omises", PlanDescriber.Describe(big, null).Contains("Top views omitted"));
}

// Modification d'un plan existant : suppressions, changements, creux, ajouts du générateur par-dessus, ids sans collision,
// porte dans un mur existant, meubles dans une pièce existante (boîte creuse ou murs pleins), niveau ajouté au-dessus.
Console.WriteLine("plan-edit");
{
    var editSchema = BuildingProgramSchema.BuildEdit(materials, doorInfos, furniture);
    File.WriteAllText(Path.Combine(outDir, "edit-schema.json"), editSchema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Check("plan-edit", "schéma ↔ PlanEdit", EditSchemaMatchesDto(editSchema), $"{editSchema.ToJsonString().Length} chars");

    var edit = PlanEditJson.Parse("""
        {
          "removeOps": ["zz"],
          "changeOps": [{ "id": "g1", "material": "BrickItem", "form": "", "rot": -1, "a": [], "b": [] }, { "id": "g2", "material": "NopeItem", "form": "", "rot": -1, "a": [], "b": [] }],
          "cuts": [{ "x": 8, "y": 11, "z": 2, "width": 1, "depth": 1, "height": 1 }],
          "removeObjects": [],
          "add": {
            "volumes": [{ "id": "annex", "x": 14, "y": 6, "width": 6, "depth": 5, "baseZ": 0, "height": 3, "wallMaterial": "LumberItem", "wallForm": "", "wallThickness": 1, "floorMaterial": "LumberItem", "ceilingMaterial": "LumberItem", "flatCeiling": false }],
            "openings": [{ "targetId": "g1", "side": "north", "offset": 6, "width": 1, "height": 2, "sill": 0, "kind": "door", "door": "HewnDoorItem" }],
            "furniture": [{ "type": "HewnTableItem", "x": 6, "y": 6, "baseZ": 0, "rot": 0 }]
          }
        }
        """);
    var before = cabin.Architecture.Ops.Count;
    var result = PlanEditor.Apply(cabin, edit, materials, doorInfos, furniture);
    foreach (var note in result.Notes) Console.WriteLine($"       note: {note}");
    var doc = result.Document;
    Check("plan-edit", "Apply → Document validé", doc is not null && PlanValidator.Validate(doc).All(i => i.Severity != IssueSeverity.Error), result.Error ?? "");
    if (doc is not null)
    {
        var ops = doc.Architecture.Ops;
        var objects = doc.AllObjects().Select(o => o.Object).ToList();
        Check("plan-edit", "plan de la page intact", cabin.Architecture.Ops.Count == before && cabin.Architecture.Ops[0].Material == "HewnLogItem");
        Check("plan-edit", "mur repeint, matériau inconnu gardé, ids inconnus notés", ops[0].Material == "BrickItem" && ops[1].Material == "HewnLogItem"
            && result.Notes.Any(n => n.Contains("« zz »")) && result.Notes.Any(n => n.Contains("NopeItem")), string.Join(" · ", result.Notes));
        Check("plan-edit", "creux c1 puis ajouts après les ops existantes", ops[before].Id == "c1" && ops[before].Subtract && ops.Skip(before + 1).Any(o => o.Material == "LumberItem"));
        Check("plan-edit", "ids uniques (ops et objets)", ops.Select(o => o.Id).Distinct().Count() == ops.Count && objects.Select(o => o.Id).Distinct().Count() == objects.Count,
            string.Join(",", ops.Select(o => o.Id)));
        var door = objects.FirstOrDefault(o => o.Type == "HewnDoorItem" && o.Id != "d1");
        Check("plan-edit", "porte dans le mur existant g1 : creusée, objet posé", door is not null && door.Id == "d2" && ops.Any(o => o.Subtract && o.A![1] == 4 && o.A[0] == 10),
            string.Join(", ", objects.Select(o => $"{o.Id} {o.Type} {o.X},{o.Y}")));
        Check("plan-edit", "table posée dans la pièce existante", objects.Any(o => o.Type == "HewnTableItem"));

        // Deuxième passe : chaise sur la table (refusée), étage posé au-dessus du dernier niveau (nouveau niveau à z 8).
        var second = PlanEditJson.Parse("""
            { "add": { "volumes": [{ "id": "top", "x": 4, "y": 4, "width": 6, "depth": 6, "baseZ": 8, "height": 3, "wallMaterial": "HewnLogItem", "floorMaterial": "HewnLogItem", "ceilingMaterial": "HewnLogItem" }],
                       "furniture": [{ "type": "HewnChairItem", "x": 6, "y": 6, "baseZ": 0, "rot": 0 }, { "type": "HewnChairItem", "x": 6, "y": 8, "baseZ": 0, "rot": 0 }] } }
            """);
        var again = PlanEditor.Apply(doc, second, materials, doorInfos, furniture);
        var doc2 = again.Document!;
        Check("plan-edit", "chaise sur la table refusée, l'autre posée", doc2 is not null && again.Notes.Any(n => n.Contains("chevauche")) && doc2.AllObjects().Count(o => o.Object.Type == "HewnChairItem") == 1,
            string.Join(" · ", again.Notes));
        Check("plan-edit", "étage au-dessus : nouveau niveau à z 8", doc2 is not null && doc2.Levels.Count == 2 && doc2.LevelBaseY(1) == 8, doc2 is null ? "" : string.Join(", ", doc2.Levels.Select((l, k) => $"{doc2.LevelBaseY(k)}/{l.Height}")));
    }

    // Pièce aux murs pleins (dessin libre) : les murs fins sont des cloisons (porte), le meuble tient sur l'emprise libre.
    var free = PlanDocument.Empty(12, 12);
    ArchOp Solid(string id, int x0, int y0, int z0, int x1, int y1, int z1) => new() { Id = id, Kind = "box", Material = "HewnLogItem", A = [x0, y0, z0], B = [x1, y1, z1] };
    free.Architecture.Ops.AddRange([Solid("a1", 1, 1, 0, 10, 10, 0), Solid("w1", 1, 1, 1, 10, 1, 3), Solid("w2", 1, 10, 1, 10, 10, 3), Solid("w3", 1, 2, 1, 1, 9, 3), Solid("w4", 10, 2, 1, 10, 9, 3), Solid("a2", 1, 1, 4, 10, 10, 4)]);
    var freeEdit = PlanEditJson.Parse("""
        { "add": { "openings": [{ "targetId": "w2", "side": "south", "offset": 3, "width": 1, "height": 2, "sill": 0, "kind": "door", "door": "" }],
                   "furniture": [{ "type": "HewnTableItem", "x": 4, "y": 4, "baseZ": 0, "rot": 0 }, { "type": "HewnChairItem", "x": 1, "y": 5, "baseZ": 0, "rot": 0 }] } }
        """);
    var freeResult = PlanEditor.Apply(free, freeEdit, materials, doorInfos, furniture);
    var freeObjects = freeResult.Document?.AllObjects().Select(o => o.Object).ToList() ?? [];
    Check("plan-edit", "murs pleins : porte dans w2, table posée, chaise dans le mur refusée", freeObjects.Count(o => o.Type == "HewnDoorItem") == 1 && freeObjects.Count(o => o.Type == "HewnTableItem") == 1 && freeObjects.All(o => o.Type != "HewnChairItem"),
        string.Join(" · ", freeResult.Notes));
}

// 8. Fidélité de la migration : chaque plan v3 figé, relu par Parse (matérialisation) et analysé, donne les chiffres de l'ancien moteur.
Console.WriteLine("fidelity");
Fidelity.Run(args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("PLANNER_BASELINE"), catalog, Check);

Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAIL");
return failures == 0 ? 0 : 1;

// Réplique de reconcileRooms (JS) : une pièce par poche d'air fermée, rangée au niveau de sa cellule la plus basse
// (graine relative à la base de ce niveau).
static void SeedRooms(PlanDocument doc, Catalog catalog)
{
    var ctx = GridBuilder.Build(doc, catalog);
    ObjectPlacer.PlaceAll(ctx);
    var n = 0;
    foreach (var pocket in PocketDetector.Detect(ctx).Pockets)
    {
        var k = doc.LevelIndexAtY(pocket.Seed.Z);
        doc.Levels[k].Rooms.Add(new PlanRoom { Id = $"r{++n}", Name = $"Room {n} (L{k})", Seed = new RoomSeed { X = pocket.Seed.X, Y = pocket.Seed.Y, Z = pocket.Seed.Z - doc.LevelBaseY(k) } });
    }
}

// Miroir de BuildingGenerator.OpenTo sur le programme brut (emprises dans la grille) : volume ouvert jusqu'à son toit.
static bool OpenToRoof(VolumeElement v, BuildingProgram p)
{
    var ceiling = v.BaseZ + v.Height + 1;
    return !v.FlatCeiling && p.Roofs.Any(r => r.Style is "gable" or "hip" && r.Courses == 0 && r.BaseZ - ceiling is 0 or 1
            && r.X <= v.X && r.Y <= v.Y && r.X + r.Width >= v.X + v.Width && r.Y + r.Depth >= v.Y + v.Depth)
        && !p.Volumes.Any(u => u.BaseZ == ceiling && u.X < v.X + v.Width && v.X < u.X + u.Width && u.Y < v.Y + v.Depth && v.Y < u.Y + u.Depth);
}

// Poteaux (ops Column) face au bâti émis avant eux : aucune cellule sur un bloc déjà posé, pied sur le terrain (z 1) ou
// sur un bloc, tête sous un toit si requireCover.
static List<string> ColumnIssues(PlanDocument doc, bool requireCover)
{
    var (w, d, h) = (doc.Grid.Width, doc.Grid.Depth, doc.Architecture.Height);
    var ops = doc.Architecture.Ops;
    var first = ops.FindIndex(op => op.Form == "Column");
    var built = new Dictionary<(int, int, int), string>();
    foreach (var op in first < 0 ? [] : ops.Take(first))
        ArchShapes.Paint(op, w, d, h, (x, y, z) => { if (op.Subtract) built.Remove((x, y, z)); else built[(x, y, z)] = op.Form ?? ""; });
    var issues = new List<string>();
    foreach (var op in ops.Where(op => op.Form == "Column"))
    {
        int x0 = op.A![0], y0 = op.A[1], z0 = op.A[2], x1 = op.B![0], y1 = op.B[1], z1 = op.B[2];
        var footprint = Enumerable.Range(x0, x1 - x0 + 1).SelectMany(x => Enumerable.Range(y0, y1 - y0 + 1).Select(y => (x, y))).ToList();
        foreach (var (x, y) in footprint)
        {
            for (var z = z0; z <= z1; z++) if (built.TryGetValue((x, y, z), out var f)) issues.Add($"{x},{y},{z} remplace {f}");
            if (requireCover && !(built.TryGetValue((x, y, z1 + 1), out var above) && above.StartsWith("Roof"))) issues.Add($"{x},{y} tête à z {z1} sans toit au-dessus");
        }
        // Appui d'un poteau de plusieurs cases : un bloc sous l'une d'elles suffit (comme le générateur).
        if (z0 < 1 || z0 > 1 && !footprint.Any(c => built.ContainsKey((c.x, c.y, z0 - 1)))) issues.Add($"{x0},{y0} pied à z {z0} sans appui");
    }
    return issues;
}

static WorldObjectInfo Piece(string name, (int X, int Y, int Z)[] cells)
    => new() { Name = name, Tier = 1, AttachedSide = "Down", MustBeGridAligned = true, Cells = cells.Select(c => new OccupancyCell(new Vec3i(c.X, c.Y, c.Z), OccupancyKind.Occupied)).ToList() };

// Chaque objet du schéma a exactement les propriétés (camelCase) du type C# correspondant.
static bool SchemaMatchesDto(System.Text.Json.Nodes.JsonObject schema)
{
    var pairs = new (string Path, Type Type)[]
    {
        ("", typeof(BuildingProgram)), ("grid", typeof(ProgramGrid)), ("defaults", typeof(ProgramDefaults)),
        ("volumes", typeof(VolumeElement)), ("slabs", typeof(SlabElement)), ("roofs", typeof(RoofElement)), ("partitions", typeof(PartitionElement)),
        ("columns", typeof(ColumnElement)), ("stairs", typeof(StairsElement)), ("openings", typeof(OpeningElement)),
        ("blocks", typeof(BlockElement)), ("furniture", typeof(FurnitureElement)), ("fences", typeof(FenceElement)),
    };
    return pairs.All(p => NodeMatches(schema, p.Path, p.Type, []));
}

// Schéma d'édition : racine = PlanEdit, changeOps = OpChange, cuts = CutElement, add = BuildingProgram sans name, grid, defaults.
static bool EditSchemaMatchesDto(System.Text.Json.Nodes.JsonObject schema)
    => NodeMatches(schema, "", typeof(PlanEdit), []) && NodeMatches(schema, "changeOps", typeof(OpChange), []) && NodeMatches(schema, "cuts", typeof(CutElement), [])
        && NodeMatches(schema, "add", typeof(BuildingProgram), ["name", "grid", "defaults"])
        && SchemaMatchesDtoElements((System.Text.Json.Nodes.JsonObject)schema["properties"]!["add"]!);

static bool SchemaMatchesDtoElements(System.Text.Json.Nodes.JsonObject add)
{
    var pairs = new (string Path, Type Type)[]
    {
        ("volumes", typeof(VolumeElement)), ("slabs", typeof(SlabElement)), ("roofs", typeof(RoofElement)), ("partitions", typeof(PartitionElement)),
        ("columns", typeof(ColumnElement)), ("stairs", typeof(StairsElement)), ("openings", typeof(OpeningElement)),
        ("blocks", typeof(BlockElement)), ("furniture", typeof(FurnitureElement)), ("fences", typeof(FenceElement)),
    };
    return pairs.All(p => NodeMatches(add, p.Path, p.Type, []));
}

static bool NodeMatches(System.Text.Json.Nodes.JsonObject schema, string path, Type type, string[] omitted)
{
    var node = path.Length == 0 ? schema : (System.Text.Json.Nodes.JsonObject)schema["properties"]![path]!;
    if (node["type"]!.GetValue<string>() == "array") node = (System.Text.Json.Nodes.JsonObject)node["items"]!;
    var props = ((System.Text.Json.Nodes.JsonObject)node["properties"]!).Select(p => p.Key).Order().ToList();
    var required = ((System.Text.Json.Nodes.JsonArray)node["required"]!).Select(r => r!.GetValue<string>()).Order().ToList();
    var dto = type.GetProperties().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).Except(omitted).Order().ToList();
    if (props.SequenceEqual(dto) && required.SequenceEqual(dto) && !node["additionalProperties"]!.GetValue<bool>()) return true;
    Console.WriteLine($"  schéma ≠ DTO pour {type.Name} : schéma [{string.Join(",", props)}] requis [{string.Join(",", required)}] dto [{string.Join(",", dto)}]");
    return false;
}
