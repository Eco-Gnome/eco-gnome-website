using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

public sealed class MaterialSlot
{
    public required string Name { get; init; }
    public int Tier { get; init; }
    public bool CountsAsWall { get; init; }
    public bool Known { get; init; }
}

public sealed class PlacedObject
{
    public required int Index { get; init; }
    public required int Level { get; init; }
    public required PlanObject Doc { get; init; }
    public WorldObjectInfo? Info { get; init; }
    public Vec3i Origin { get; set; }
    public int Rotation { get; init; }
    public bool Placed { get; set; }
    public bool IsDoorCarving { get; set; }
    public int? ParentIndex { get; set; }
    public int Depth { get; set; }
    public List<(Vec3i Pos, OccupancyKind Kind)> Cells { get; } = [];
    public string? RoomId { get; set; }
    public int MaxDy { get; set; }     // plus haute cellule relative à l'origine (pour empiler dessus)
}

// État partagé de l'analyse : grille voxel, matériaux indexés, empreintes 2D, objets posés, problèmes.
public sealed class BuildContext
{
    public required PlanDocument Document { get; init; }
    public required Catalog Catalog { get; init; }
    public required VoxelGrid Grid { get; init; }
    public List<MaterialSlot> Materials { get; } = [];
    public Dictionary<string, int> MaterialIndexByName { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, HashSet<(int X, int Y)>> RoomFootprints { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> RoomLevel { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> RoomCeilingY { get; } = new(StringComparer.Ordinal);
    public List<PlacedObject> Objects { get; } = [];
    public List<PlanIssue> Issues { get; } = [];

    public int GetOrAddMaterial(string name)
    {
        if (MaterialIndexByName.TryGetValue(name, out var index)) return index;

        var info = Catalog.GetMaterial(name);
        if (info is null) Issues.Add(PlanIssue.Warning("UnknownMaterial", [name]));

        index = Materials.Count;
        Materials.Add(new MaterialSlot
        {
            Name = name,
            Tier = info?.Tier ?? 0,
            CountsAsWall = info?.CountsAsWall ?? true,
            Known = info is not null,
        });
        MaterialIndexByName[name] = index;
        return index;
    }

    // Bloc de mur posé par le plan (ni dalle ni plafond) : ce qu'une porte peut creuser.
    public static bool IsWallBlock(Voxel v) => v.Kind == VoxelKind.Block && !v.IsFloor && !v.IsCeiling;

    public bool IsWallVoxel(Voxel v) => v.Kind switch
    {
        VoxelKind.Terrain => true,
        VoxelKind.Block => v.MaterialIndex >= 0 && Materials[v.MaterialIndex].CountsAsWall,
        VoxelKind.ObjectWall => true,
        _ => false,
    };

    // Tier ajouté à la composition pour un voxel-mur ; null = compte dans WallCount mais pas dans la composition
    // (objet-mur sans tier, comme dans RoomChecker.AddWorldObjectToRoom).
    public float? WallTier(Voxel v) => v.Kind switch
    {
        VoxelKind.Terrain => 0f,
        VoxelKind.Block => Materials[v.MaterialIndex].Tier,   // + PaintedBlockTierBonus si peint : non modélisé en v1
        VoxelKind.ObjectWall => Objects[v.ObjectIndex].Info?.Tier,
        _ => null,
    };
}
