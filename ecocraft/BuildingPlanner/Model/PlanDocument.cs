using System.Text.Json;
using System.Text.Json.Serialization;

namespace ecocraft.BuildingPlanner.Model;

// Contrat du document de plan, partagé avec l'îlot JavaScript (JSON camelCase). Les références aux données du
// jeu se font par Name technique (ex. « LumberItem »), jamais par Guid. Coordonnées du plan : x → Eco X,
// y → Eco Z, z → Eco Y (vertical). Tout le bâti est dans Architecture.Ops (formes 3D évaluées en blocs unitaires,
// posées sur le terrain à z = 0) : ni sol ni plafond implicites, comme en jeu. Une pièce est une poche d'air fermée
// par des blocs, désignée par sa graine. Les niveaux sont des tranches d'affichage et de pose : le niveau k occupe
// les couches base_k .. base_k + hauteur_k, z = 1 y est la première couche au-dessus de sa base ; ils portent les
// pièces et les objets. Mode = vue de l'îlot (Structure / Aménagement), l'analyse est la même dans les deux.
// Un document de schéma < 4 est migré par PlanDocumentJson.Parse (LegacyPlanV3).
public sealed class PlanDocument
{
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Name { get; set; } = "";
    public string Mode { get; set; } = PlanMode.House;             // PlanMode.House | PlanMode.Architecture
    public GridSize Grid { get; set; } = new();
    public ArchitecturePlan Architecture { get; set; } = new();
    public PlanDefaults Defaults { get; set; } = new();
    public List<PlanLevel> Levels { get; set; } = [];
    public int GroundIndex { get; set; }          // index du niveau posé au sol ; numéro affiché = k − GroundIndex, index < GroundIndex = sous-sol
    public AnalysisOptions Analysis { get; set; } = new();
    public Dictionary<string, decimal> Prices { get; set; } = new();   // prix unitaire saisi par Name d'item ; absent → moyenne des prix du serveur

    [JsonIgnore] public bool IsArchitecture => Mode == PlanMode.Architecture;

    public static PlanDocument Empty(int width = 25, int depth = 20) => new() { Grid = new GridSize { Width = width, Depth = depth }, Levels = [new PlanLevel()] };

    // Normalisation après lecture (schéma < 4 déjà migré par LegacyPlanV3) : au moins un niveau ; v2 → mode maison.
    public void Migrate()
    {
        if (Levels.Count == 0) Levels.Add(new PlanLevel());
        GroundIndex = Math.Clamp(GroundIndex, 0, Levels.Count - 1);
        if (Mode != PlanMode.Architecture) Mode = PlanMode.House;
        SchemaVersion = CurrentSchemaVersion;
    }

    public int LevelHeight(int level) => Levels[level].Height ?? Defaults.WallHeight;

    // Numéro de niveau tel qu'affiché à l'utilisateur (négatif pour un sous-sol).
    public int DisplayNumber(int level) => level - GroundIndex;

    // Y de la dalle du niveau : 0 au sol, puis somme des (hauteur + dalle) des niveaux inférieurs.
    public int LevelBaseY(int level)
    {
        var y = 0;
        for (var i = 0; i < level; i++) y += LevelHeight(i) + 1;
        return y;
    }

    // Niveau dont la tranche [base_k, base_{k+1}) contient y ; le dernier niveau absorbe tout ce qui est au-dessus.
    public int LevelIndexAtY(int y)
    {
        for (var k = Levels.Count - 1; k >= 0; k--)
            if (y >= LevelBaseY(k)) return k;
        return 0;
    }

    public IEnumerable<(int Level, PlanRoom Room)> AllRooms() => Levels.SelectMany((l, k) => l.Rooms.Select(r => (k, r)));
    public IEnumerable<(int Level, PlanObject Object)> AllObjects() => Levels.SelectMany((l, k) => l.Objects.Select(o => (k, o)));

    public (int Level, PlanRoom Room)? FindRoom(string? id)
    {
        if (id is null) return null;
        foreach (var entry in AllRooms()) if (entry.Room.Id == id) return entry;
        return null;
    }

    public (int Level, PlanObject Object)? FindObject(string? id)
    {
        if (id is null) return null;
        foreach (var entry in AllObjects()) if (entry.Object.Id == id) return entry;
        return null;
    }
}

public sealed class GridSize
{
    public int Width { get; set; } = 25;
    public int Depth { get; set; } = 20;
}

public sealed class PlanDefaults
{
    public int WallHeight { get; set; } = 3;      // hauteur d'un niveau sans surcharge
}

public sealed class PlanLevel
{
    public string Name { get; set; } = "";        // vide → libellé par défaut dans l'UI
    public int? Height { get; set; }              // couches au-dessus de la base ; null → Defaults.WallHeight
    public List<PlanRoom> Rooms { get; set; } = [];
    public List<PlanObject> Objects { get; set; } = [];
}

public sealed class GridPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

// Graine d'une pièce : Z relatif à la base de son niveau, comme PlanObject.Z (1 = première couche au-dessus).
public sealed class RoomSeed
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; } = 1;
}

public sealed class PlanRoom
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public RoomSeed Seed { get; set; } = new();   // cellule d'air de la poche ; niveau de la pièce = celui de sa cellule la plus basse
    public string? LockCategory { get; set; }     // catégorie housing forcée (comme dans le jeu), sinon estimée
}

public sealed class PlanObject
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int? Z { get; set; }                   // hauteur de l'origine, relative au niveau ; null → posé sur ce qu'il y a dessous
    public int Rotation { get; set; }             // quarts de tour, 0..3
    public string? AttachedTo { get; set; }       // empilé sur l'objet (HasTableSurface) — fixé par l'UI au drop
}

public sealed class AnalysisOptions
{
    public int Residents { get; set; } = 1;
    public string PropertyType { get; set; } = "Residence";
}

public static class PlanMode
{
    public const string House = "house";
    public const string Architecture = "architecture";
}

// Bâti : formes évaluées dans l'ordre en blocs unitaires ; les cellules hors grille ou au-dessus de
// Height sont rognées à l'évaluation (jamais rejetées), un redimensionnement ne perd donc rien.
public sealed class ArchitecturePlan
{
    public int Height { get; set; } = 20;                   // couches z, 1..PlanValidator.MaxArchitectureHeight
    public List<ArchOp> Ops { get; set; } = [];
    public ImagePlacement? Image { get; set; }              // placement du fond de plan ; les pixels sont dans BuildingPlan.BackgroundImage
}

// Une seule représentation géométrique : la boîte englobante inclusive A..B (ordre libre). Le diamètre d'une
// sphère ou d'un cylindre est l'étendue de la boîte par axe (centre demi-entier pour un diamètre pair).
public sealed class ArchOp
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";                  // box | sphere | cylinder | line | curve | cells
    public bool Subtract { get; set; }                      // false → pose Material ; true → vide
    public string? Material { get; set; }                   // requis si !Subtract
    public int[]? A { get; set; }                           // [x,y,z] coin (box/sphere/cylinder) ou extrémité (line/curve)
    public int[]? B { get; set; }
    public int[]? C { get; set; }                           // curve : point de contrôle de la Bézier quadratique A → B
    public string? Axis { get; set; }                       // cylinder : x | y | z (défaut z) ; disque = cylindre avec a.z == b.z
    public bool Hollow { get; set; }                        // box (4 murs), sphere (coque), cylinder (tube)
    public bool Closed { get; set; }                        // Hollow box / cylinder : fermé aux deux bouts (pavé creux, tube bouché)
    public int Thickness { get; set; } = 1;                 // épaisseur de la coque si Hollow
    public int[]? Cells { get; set; }                       // cells : triplets aplatis [x,y,z, x,y,z, …]
    // Forme du jeu portée par chaque cellule de l'op (Wall, Floor, RoofSide, RoofCorner, RoofTurn, RoofPeak, Stairs,
    // Column, Cube) et rotation 0..3 : lues par l'affichage seulement (3D, icône et côté bas en 2D). Pour les pièces et le coût une forme reste un
    // cube plein, comme en jeu.
    public string? Form { get; set; }
    public int? Rot { get; set; }
}

public sealed class ImagePlacement
{
    public double X { get; set; }                           // coin haut-gauche en cellules (fractionnaire autorisé)
    public double Y { get; set; }
    public double Width { get; set; } = 20;                 // largeur en cellules ; hauteur déduite du ratio de l'image
    public double Opacity { get; set; } = 0.5;
}

public static class PlanDocumentJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = false,
    };

    public static PlanDocument Parse(string json)
    {
        var document = JsonSerializer.Deserialize<PlanDocument>(json, Options);
        if (document is null) throw new JsonException("Empty plan document.");
        if (document.SchemaVersion < PlanDocument.CurrentSchemaVersion)
            LegacyPlanV3.Migrate(JsonSerializer.Deserialize<LegacyPlanV3>(json, Options)!, document);
        document.Migrate();
        return document;
    }

    public static PlanDocument? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return Parse(json); }
        catch (JsonException) { return null; }
    }

    public static string Serialize(PlanDocument document) => JsonSerializer.Serialize(document, Options);

    // Fragments du document (pièce, objet, niveau) échangés avec l'îlot JS, mêmes conventions JSON.
    public static string SerializePart<T>(T part) => JsonSerializer.Serialize(part, Options);
}
