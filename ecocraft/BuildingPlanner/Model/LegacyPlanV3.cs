namespace ecocraft.BuildingPlanner.Model;

// Données des schémas 1 à 3 que le schéma 4 n'a plus : sol et plafond par défaut, murs / sols / trous par niveau (à la
// racine en v1), hauteur et plafond des pièces. Relues une seconde fois par PlanDocumentJson.Parse, jamais réécrites :
// la migration matérialise le sol et les plafonds implicites de l'ancien moteur en ops `cells` placées en tête du bâti
// (les ops de l'utilisateur continuent de l'emporter), pour que le plan garde exactement son score.
public sealed class LegacyPlanV3
{
    public LegacyDefaults Defaults { get; set; } = new();
    public List<LegacyLevel> Levels { get; set; } = [];

    // Schéma 1 : un seul niveau, collections à la racine.
    public Dictionary<string, LegacyWall>? Walls { get; set; }
    public Dictionary<string, string>? Floors { get; set; }
    public List<LegacyRoom>? Rooms { get; set; }
    public List<PlanObject>? Objects { get; set; }

    public static void Migrate(LegacyPlanV3 legacy, PlanDocument doc)
    {
        var levels = legacy.Levels;
        if (doc.Levels.Count == 0)
        {
            var rooms = legacy.Rooms ?? [];
            doc.Levels.Add(new PlanLevel
            {
                Rooms = rooms.Select(r => new PlanRoom { Id = r.Id, Name = r.Name, Seed = new RoomSeed { X = r.Seed.X, Y = r.Seed.Y }, LockCategory = r.LockCategory }).ToList(),
                Objects = legacy.Objects ?? [],
            });
            levels = [new LegacyLevel { Walls = legacy.Walls ?? new(), Floors = legacy.Floors ?? new(), Rooms = rooms }];
        }

        var ops = LegacyMaterializer.Materialize(doc, levels, legacy.Defaults);
        if (ops.Count == 0) return;
        doc.Architecture.Ops.InsertRange(0, ops);
        var top = ops.Max(ArchShapes.MaxZ) + 1;
        doc.Architecture.Height = Math.Min(Math.Max(doc.Architecture.Height, top), PlanValidator.MaxArchitectureHeight);
    }
}

public sealed class LegacyDefaults
{
    public string? FloorMaterial { get; set; }    // null → terrain (tier 0) ; niveau 0 seulement
    public string? CeilingMaterial { get; set; }
}

public sealed class LegacyLevel
{
    public Dictionary<string, LegacyWall> Walls { get; set; } = new();      // clé « x,y »
    public Dictionary<string, string> Floors { get; set; } = new();         // clé « x,y » → matériau (niveau 0 : surcharge du sol par défaut ; étages : dalle explicite)
    public Dictionary<string, bool> Holes { get; set; } = new();            // clé « x,y » : ouverture dans la dalle (étages seulement)
    public List<LegacyRoom> Rooms { get; set; } = [];
}

public sealed class LegacyWall
{
    public string Material { get; set; } = "";
    public int? Height { get; set; }              // surcharge ; sinon hauteur des pièces adjacentes puis celle du niveau
}

public sealed class LegacyRoom
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public GridPoint Seed { get; set; } = new();
    public int? Height { get; set; }              // couches d'air ; null → hauteur du niveau ; plafond posé à z = hauteur + 1
    public string? CeilingMaterial { get; set; }
    public string? LockCategory { get; set; }
}

public static class PlanKeys
{
    public static bool TryParse(string key, out int x, out int y)
    {
        x = y = 0;
        var comma = key.IndexOf(',');
        if (comma <= 0 || comma >= key.Length - 1) return false;
        return int.TryParse(key.AsSpan(0, comma), out x) && int.TryParse(key.AsSpan(comma + 1), out y);
    }
}
