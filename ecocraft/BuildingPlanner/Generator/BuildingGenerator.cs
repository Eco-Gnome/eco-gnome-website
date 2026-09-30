using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner.Generator;

// Programme architectural → PlanDocument en mode maison : tout le bâti en ops (avec Form/Rot pour le rendu), dalle du
// rez comprise, pile de niveaux déduite des dalles des volumes, aucune pièce (le canvas les crée par poche d'air fermée). Pur et
// déterministe : identifiants « g1, g2… », ordre d'émission volumes → plateaux → toits → cloisons → poteaux →
// escaliers → blocs → clôtures → ouvertures. Tout est re-clampé ; chaque ajustement laisse une note. Une catégorie qui dépasse le
// budget est abandonnée entière (les ouvertures sont réservées d'avance). Le document est validé avant d'être rendu.
public static partial class BuildingGenerator
{
    public const int OpBudget = 460;
    public const long WorkBudget = 40_000_000;
    public const int MinGrid = 8, MaxGrid = 120;
    public const int MaxBaseZ = 300;
    public const int MaxStoreyHeight = 12;
    public const int MaxCourses = 20;
    public const int MaxItems = 40;
    public const int MaxBlocks = 80;   // une op par bloc : un quai à pieux isolés dépasse vite 40 entrées
    public const int ColumnSnap = 2;   // écart (couches) comblé entre un poteau et son appui ou le bloc qui le couvre
    // Même liste et même ordre que FORMS dans building-planner.js (le code de forme d'une cellule en dépend).
    public static readonly string[] Forms = ["Wall", "Floor", "RoofSide", "RoofCorner", "RoofTurn", "RoofPeak", "Stairs", "Column", "Cube", "WindowGrilles",
        "DocksPlatform", "DocksPlatformFill", "DocksColumn", "DocksPillar", "DocksPillarBeam", "DocksPillarBeamCorner", "DocksPillarBeamEnd", "DocksPillarBeamEndAlt",
        "DocksPillarBeamJunction", "DocksPillarBeamT", "DocksPillarBeamX", "DocksFenceMid", "DocksFenceCorner", "DocksFenceT", "DocksFenceX", "DocksFenceEndCap",
        "DocksFenceEndCapDouble", "DocksFenceSolo", "DocksRamps", "DocksRampsCorner", "DocksRampsCornerInverted", "DocksRampA", "DocksRampB", "DocksRampC", "DocksRampD",
        "DocksBarrelPlatform", "Ladder", "Stacked1", "Stacked2", "Stacked3", "Chimney", "CopperPipe", "IronPipe",
        "Wall_01", "Wall_02", "Wall_03", "Wall_04", "Wall_05", "Wall_06", "Wall_07", "Wall_08", "Wall_09", "Wall_10", "Wall_11", "Wall_12", "Wall_13", "Wall_14",
        "Wall_15", "Wall_16", "Wall_17", "Wall_18", "Wall_19", "WallCorner_01", "WallCorner_02", "WallCorner_03", "WallCorner_04", "WallT_01", "WallT_02", "WallT_03",
        "WallT_04", "WallX_01", "WallX_02", "WallX_03", "WallX_04", "Column_01", "Column_02", "Column_03", "Ceiling", "Window", "FenceMid", "FenceCorner", "FenceT",
        "FenceX", "FenceSolo", "FenceEnd", "StairsMid", "StairsEndLeft", "StairsEndRight", "StairsTurn", "StairsCorner", "RoofEdgeSide", "RoofEdgeCorner", "RoofEdgeTurn",
        "RoofPeakCorner", "RoofPeakT", "RoofPeakX", "RoofUnderslopeSide", "RoofUnderslopeTurn", "RoofUnderslopeCorner", "UnderInnerPeak", "RoofCube", "Roof", "RoofPeakSet",
        "FlatRoof", "ThinFloorTop", "ThinFloorBottom", "ThinWallStraight", "ThinWallCorner", "EdgeWall", "EdgeWallTurn", "Stacked4",
        "Aqueduct", "BasicSlopePoint", "BasicSlopeSide", "UnderSlopePeak", "UnderSlopeSide", "Brace", "BraceCorner", "BraceTurn", "SideBrace",
        "SmallCornerBrace", "UnderBrace", "UnderBraceCorner", "UnderBraceTurn", "ThinWallEdge", "WindowEdge", "WindowGrillesEdge", "RampA", "RampB",
        "RampC", "RampD",
        "RoadBarrier", "Fence", "DoubleWindow", "FloatStairs", "FloatStairsTurn", "FloatStairsCorner", "ThinColumn", "UnderPeakSet", "PeakSet",
        "UnderStairs", "BasicSlopeCorner", "BasicSlopeTurn", "UnderSlopeCorner", "UnderSlopeTurn", "HalfSlopeA", "HalfSlopeB",
        "FullWall", "WindowWall", "CladWall", "WallTrim", "SideFence", "WindowCorners",
        "WallSolo", "WallMid", "WallEnd", "WallT", "WallCorner", "WallX", "RoofSolo", "RoofMid", "RoofEnd", "RoofT", "RoofX", "RoofFill", "StairsSolo",
        "WhiteCube", "WhiteLine", "WhiteDashLine", "WhiteEdge", "WhiteEdgeRotate", "TwoWhiteEdgeRotate", "WhiteRampLineA", "WhiteRampLineB",
        "WhiteRampLineC", "WhiteRampLineD", "WhiteRampDashLineA", "WhiteRampDashLineB", "WhiteRampDashLineC", "WhiteRampDashLineD", "WhiteRampEdgeA",
        "WhiteRampEdgeB", "WhiteRampEdgeC", "WhiteRampEdgeD", "SimpleFloor", "CanopyWindow", "SteelPipe"];

    // dir(d) : east, south, west, north (index de direction du planner, celui des programmes).
    private static readonly (int X, int Y)[] Dir = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    // Rot d'une op = rotation du bloc en jeu (0..3, celle des bundles de formes). Le jeu tourne en sens inverse du planner :
    // le côté bas de RoofSide / Stairs regarde E, N, W, S pour rot 0..3 — mesuré sur les meshes extraits (Scripts/forms ;
    // RoofCorner : CornerRot). Rotation dont le côté bas regarde dir(d) :
    private static int LowToward(int d) => (4 - d) % 4;

    // doorTypes : objets porte du catalogue (nom + cellules structurantes), du plus bas tier au plus haut ; chaque embrasure
    // reçoit une porte à sa vraie taille : celle demandée par le programme, sinon celle de la famille de son mur
    // (« HewnLogItem » → « HewnDoorItem »), sinon la première qui tient dans le mur.
    // Liste vide → embrasures vides, la pièce reste ouverte en 3D tant que le joueur n'y pose pas une porte.
    // furniture : meubles que le programme peut poser (nom + cellules d'occupancy du catalogue) ; un type absent est ignoré.
    // baseDoc : plan existant à compléter (modification par l'IA) ; ses ops, niveaux et objets sont gardés, les ops du
    // programme s'ajoutent après les siennes, sa grille remplace celle du programme (voir Build.Seeded).
    public static GeneratorResult Generate(BuildingProgram program, IReadOnlyCollection<string> allowedMaterials, IReadOnlyList<FurnitureInfo>? doorTypes = null, IReadOnlyCollection<FurnitureInfo>? furniture = null, PlanDocument? baseDoc = null)
    {
        var b = new Build(program, allowedMaterials, doorTypes, furniture, baseDoc);
        if (b.Allowed.Count == 0) return new GeneratorResult(null, b.Notes, "NoMaterials");

        var volumes = b.NormalizeVolumes();
        var partitions = b.NormalizePartitions();
        var roofs = b.NormalizeRoofs();
        b.BuildLevels(volumes);
        var (walls, inner) = b.ExistingWalls();
        var openings = b.BuildOpenings([.. volumes, .. walls], [.. partitions, .. inner]);
        b.PlaceFurniture([.. volumes, .. walls], [.. partitions, .. inner]);
        b.Reserve(openings);

        b.EmitVolumes(volumes, roofs);
        b.EmitSlabs();
        b.EmitRoofs(roofs, volumes, partitions);
        b.EmitPartitions(partitions, volumes);
        b.EmitColumns();
        b.EmitStairs();
        b.EmitBlocks();
        b.EmitFences();
        b.Reserve([]);
        b.EmitCategory("ouvertures", openings.Select(o => (List<ArchOp>)[o]));

        var doc = b.Doc;
        if (doc.Architecture.Ops.Count == 0 && baseDoc is null) return new GeneratorResult(null, b.Notes, "EmptyProgram");
        var maxZ = doc.Architecture.Ops.Select(ArchShapes.MaxZ).DefaultIfEmpty(0).Max();
        doc.Architecture.Height = Math.Clamp(Math.Max(maxZ + 2, baseDoc?.Architecture.Height ?? 0), 1, PlanValidator.MaxArchitectureHeight);

        var errors = PlanValidator.Validate(doc).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Code).Distinct().ToList();
        return errors.Count > 0 ? new GeneratorResult(null, b.Notes, string.Join(", ", errors)) : new GeneratorResult(doc, b.Notes, null);
    }

    private readonly record struct Rect(int X0, int Y0, int X1, int Y1);

    private sealed record Volume(string Id, Rect R, int BaseZ, int Height, int Thickness, string Wall, string WallForm, string Floor, string Ceiling, bool FlatCeiling);
    private sealed record Partition(string Id, Rect R, int BaseZ, int Height, string Material, string WallForm, bool AlongX);

    // État d'une génération : document en cours, budget, matériaux autorisés, notes.
    private sealed partial class Build
    {
        public readonly BuildingProgram P;
        public readonly HashSet<string> Allowed;
        public readonly List<string> Notes = [];
        public readonly PlanDocument Doc;
        public readonly int W, D;
        public readonly string Fallback;
        public readonly IReadOnlyList<FurnitureInfo> DoorInfos;
        public readonly IReadOnlyList<string> Doors;
        public readonly Dictionary<int, HashSet<(int X, int Y)>> DoorFronts = [];   // par baseZ : cases devant et derrière chaque porte
        public readonly Dictionary<int, HashSet<(int X, int Y)>> DoorCells = [];    // par baseZ : cases des embrasures (recouvrement)
        public readonly Dictionary<string, FurnitureInfo> Furniture;
        public List<int> Bases = [0];
        // Plan existant complété : ses ops restent en tête (les nouvelles passent par-dessus), ses niveaux et ses objets
        // sont gardés, les identifiants nouveaux évitent les siens et les budgets comptent ce qu'il contient déjà.
        public readonly bool Seeded;
        private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);
        private int _nextId, _nextDoor, _nextFurniture;
        private long _work, _reservedWork;
        private int _reservedOps;

        public Build(BuildingProgram p, IReadOnlyCollection<string> allowed, IReadOnlyList<FurnitureInfo>? doorTypes, IReadOnlyCollection<FurnitureInfo>? furniture, PlanDocument? baseDoc)
        {
            P = p;
            DoorInfos = (doorTypes ?? []).Where(d => !string.IsNullOrWhiteSpace(d.Name)).DistinctBy(d => d.Name).ToList();
            Doors = DoorInfos.Select(d => d.Name).ToList();
            Furniture = (furniture ?? []).Where(f => !string.IsNullOrWhiteSpace(f.Name)).DistinctBy(f => f.Name).ToDictionary(f => f.Name, StringComparer.Ordinal);
            Allowed = new HashSet<string>(allowed.Where(m => !string.IsNullOrWhiteSpace(m)), StringComparer.Ordinal);
            Fallback = Allowed.Contains(p.Defaults.WallMaterial) ? p.Defaults.WallMaterial : allowed.FirstOrDefault(Allowed.Contains) ?? "";
            if (baseDoc is not null)
            {
                Seeded = true;
                Doc = PlanDocumentJson.Parse(PlanDocumentJson.Serialize(baseDoc));   // copie : le plan de la page ne bouge pas
                (W, D) = (Doc.Grid.Width, Doc.Grid.Depth);
                _usedIds.UnionWith(Doc.Architecture.Ops.Select(o => o.Id));
                _usedIds.UnionWith(Doc.AllObjects().Select(o => o.Object.Id));
                _work = Doc.Architecture.Ops.Sum(Work);
                return;
            }
            W = Math.Clamp(p.Grid.Width, MinGrid, MaxGrid);
            D = Math.Clamp(p.Grid.Depth, MinGrid, MaxGrid);
            if (W != p.Grid.Width || D != p.Grid.Depth) Notes.Add($"grille ramenée à {W}×{D}");
            Doc = new PlanDocument
            {
                Name = p.Name.Trim(),
                Mode = PlanMode.House,
                Grid = new GridSize { Width = W, Depth = D },
                Defaults = new PlanDefaults { WallHeight = 3 },
            };
        }

        // Identifiant « prefix + n » libre (seuls ceux d'un plan existant peuvent déjà être pris).
        private string Fresh(string prefix, ref int counter)
        {
            string id;
            do id = $"{prefix}{++counter}"; while (!_usedIds.Add(id));
            return id;
        }

        public string Material(string? name, string what)
        {
            if (name is not null && Allowed.Contains(name)) return name;
            Notes.Add($"{what} : matériau « {name} » inconnu, remplacé par {Fallback}");
            return Fallback;
        }

        // Emprise clampée dans la grille (taille minimale respectée, position décalée si besoin).
        public Rect ClampRect(int x, int y, int width, int depth, int minSize, string what)
        {
            var w = Math.Clamp(width, minSize, W);
            var d = Math.Clamp(depth, minSize, D);
            var x0 = Math.Clamp(x, 0, W - w);
            var y0 = Math.Clamp(y, 0, D - d);
            if (w != width || d != depth || x0 != x || y0 != y) Notes.Add($"{what} : emprise ramenée dans la grille ({x0},{y0} {w}×{d})");
            return new Rect(x0, y0, x0 + w - 1, y0 + d - 1);
        }

        // Au plus MaxItems éléments par catégorie.
        public List<T> Take<T>(List<T> items, string what, int max = MaxItems)
        {
            if (items.Count <= max) return items;
            Notes.Add($"{what} : {items.Count - max} élément(s) au-delà de {max} ignoré(s)");
            return items.Take(max).ToList();
        }

        // Budget mis de côté pour les ouvertures, émises en dernier.
        public void Reserve(List<ArchOp> ops)
        {
            _reservedOps = ops.Count;
            _reservedWork = ops.Sum(Work);
        }

        // Émet chaque élément (liste d'ops) en bloc ; au premier refus la catégorie entière est abandonnée.
        public void EmitCategory(string what, IEnumerable<List<ArchOp>> elements)
        {
            var index = 0;
            foreach (var ops in elements)
            {
                if (!Commit(ops)) { Notes.Add($"{what} : budget dépassé, éléments à partir du n° {index + 1} abandonnés"); return; }
                index++;
            }
        }

        private bool Commit(List<ArchOp> ops)
        {
            var work = ops.Sum(Work);
            if (Doc.Architecture.Ops.Count + ops.Count + _reservedOps > OpBudget || _work + work + _reservedWork > WorkBudget) return false;
            foreach (var op in ops) { op.Id = Fresh("g", ref _nextId); Doc.Architecture.Ops.Add(op); }
            _work += work;
            return true;
        }

        private long Work(ArchOp op) => ArchShapes.WorkVolume(op, W, D, PlanValidator.MaxArchitectureHeight);

        public string NextDoorId() => Fresh("d", ref _nextDoor);

        public static string DoorFamily(string wallMaterial) => wallMaterial.EndsWith("Item", StringComparison.Ordinal) ? wallMaterial[..^4] + "DoorItem" : "";
        public string NextFurnitureId() => Fresh("f", ref _nextFurniture);

        // Porte d'une embrasure : celle demandée si le catalogue la connaît, sinon celle de la famille du mur, sinon la première.
        public string? DoorFor(string requested, string wallMaterial, string what)
        {
            if (Doors.Count == 0) return null;
            if (Doors.Contains(requested, StringComparer.Ordinal)) return requested;
            var family = DoorFamily(wallMaterial);
            var door = Doors.Contains(family, StringComparer.Ordinal) ? family : Doors[0];
            if (requested.Length > 0) Notes.Add($"{what} : porte « {requested} » inconnue, remplacée par {door}");
            return door;
        }
    }

    private static ArchOp Box(int x0, int y0, int z0, int x1, int y1, int z1, string material, string form, int rot = 0, int hollow = 0)
        => new() { Kind = "box", Material = material, A = [x0, y0, z0], B = [x1, y1, z1], Hollow = hollow > 0, Thickness = Math.Max(1, hollow), Form = form, Rot = rot };

    private static ArchOp Box(Rect r, int z0, int z1, string material, string form, int rot = 0, int hollow = 0) => Box(r.X0, r.Y0, z0, r.X1, r.Y1, z1, material, form, rot, hollow);

    private static ArchOp Cut(int x0, int y0, int z0, int x1, int y1, int z1) => new() { Kind = "box", Subtract = true, A = [x0, y0, z0], B = [x1, y1, z1] };

    private static ArchOp Cells(List<int> cells, string material, string form, int rot) => new() { Kind = "cells", Material = material, Cells = cells.ToArray(), Form = form, Rot = rot };

    private static int DirIndex(string? direction) => direction switch { "south" => 1, "west" => 2, "north" => 3, _ => 0 };
}

// Meuble proposé au programme : nom technique de l'objet et cellules d'occupancy structurantes (Occupied / Wall / Solid)
// du catalogue, repère Eco (X, Y vers le haut, Z = y du plan), rotation 0. Size = emprise largeur × profondeur × hauteur.
public sealed record FurnitureInfo(string Name, IReadOnlyList<Vec3i> Cells)
{
    public (int W, int D, int H) Size => Cells.Count == 0 ? (1, 1, 1)
        : (Cells.Max(c => c.X) - Cells.Min(c => c.X) + 1, Cells.Max(c => c.Z) - Cells.Min(c => c.Z) + 1, Cells.Max(c => c.Y) - Cells.Min(c => c.Y) + 1);
}
