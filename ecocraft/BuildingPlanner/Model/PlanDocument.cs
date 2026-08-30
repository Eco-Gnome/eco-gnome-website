using System.Text.Json;
using System.Text.Json.Serialization;

namespace ecocraft.BuildingPlanner.Model;

// Contrat du document de plan, partagé avec l'îlot JavaScript (JSON camelCase). Les références aux données du
// jeu se font par Name technique (ex. « LumberItem »), jamais par Guid. Coordonnées du plan : x → Eco X,
// y → Eco Z, z → Eco Y (vertical). Le bâtiment est une pile de niveaux : le niveau k occupe les couches
// Y = base_k (sa dalle) .. base_k + hauteur_k (air) ; la dalle du niveau k+1 est le plafond du niveau k.
// Dans un niveau, z = 1 est la première couche d'air au-dessus de sa dalle.
public sealed class PlanDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Name { get; set; } = "";
    public GridSize Grid { get; set; } = new();
    public PlanDefaults Defaults { get; set; } = new();
    public List<PlanLevel> Levels { get; set; } = [];
    public AnalysisOptions Analysis { get; set; } = new();

    // Schéma 1 : un seul niveau, collections à la racine. Relues pour la migration, jamais réécrites.
    public Dictionary<string, WallCell>? Walls { get; set; }
    public Dictionary<string, string>? Floors { get; set; }
    public List<PlanRoom>? Rooms { get; set; }
    public List<PlanObject>? Objects { get; set; }

    public static PlanDocument Empty(int width = 25, int depth = 20) => new() { Grid = new GridSize { Width = width, Depth = depth }, Levels = [new PlanLevel()] };

    // Document v1 (ou sans niveau) → un niveau 0 contenant les collections racine.
    public void Migrate()
    {
        if (Levels.Count == 0)
            Levels.Add(new PlanLevel { Walls = Walls ?? new(), Floors = Floors ?? new(), Rooms = Rooms ?? [], Objects = Objects ?? [] });
        Walls = null; Floors = null; Rooms = null; Objects = null;
        SchemaVersion = CurrentSchemaVersion;
    }

    public int LevelHeight(int level) => Levels[level].Height ?? Defaults.WallHeight;

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

    // Hauteur effective d'une pièce : surcharge, sinon la hauteur de son niveau (changer le défaut suit partout).
    public int RoomHeight(int level, PlanRoom room) => room.Height ?? LevelHeight(level);

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
    public int WallHeight { get; set; } = 3;
    public string? FloorMaterial { get; set; }    // null → terrain (tier 0) ; niveau 0 seulement
    public string? CeilingMaterial { get; set; }
}

public sealed class PlanLevel
{
    public string Name { get; set; } = "";                                  // vide → libellé par défaut dans l'UI
    public int? Height { get; set; }                                        // couches d'air ; null → Defaults.WallHeight
    public Dictionary<string, WallCell> Walls { get; set; } = new();        // clé « x,y »
    public Dictionary<string, string> Floors { get; set; } = new();         // clé « x,y » → matériau (niveau 0 : surcharge du sol par défaut ; étages : dalle explicite)
    public Dictionary<string, bool> Holes { get; set; } = new();            // clé « x,y » : ouverture dans la dalle (étages seulement)
    public List<PlanRoom> Rooms { get; set; } = [];
    public List<PlanObject> Objects { get; set; } = [];
}

public sealed class WallCell
{
    public string Material { get; set; } = "";
    public int? Height { get; set; }              // surcharge ; sinon hauteur des pièces adjacentes puis celle du niveau
}

public sealed class GridPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class PlanRoom
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public GridPoint Seed { get; set; } = new();
    public int? Height { get; set; }              // couches d'air ; null → hauteur du niveau ; plafond posé à z = hauteur + 1
    public string? CeilingMaterial { get; set; }
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
    public float? TargetHousing { get; set; }
    public string PropertyType { get; set; } = "Residence";
}

public static class PlanKeys
{
    public static string Make(int x, int y) => $"{x},{y}";

    public static bool TryParse(string key, out int x, out int y)
    {
        x = y = 0;
        var comma = key.IndexOf(',');
        if (comma <= 0 || comma >= key.Length - 1) return false;
        return int.TryParse(key.AsSpan(0, comma), out x) && int.TryParse(key.AsSpan(comma + 1), out y);
    }
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
