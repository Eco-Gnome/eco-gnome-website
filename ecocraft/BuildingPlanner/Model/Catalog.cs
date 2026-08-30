namespace ecocraft.BuildingPlanner.Model;

public enum OccupancyKind { Occupied, Wall, Solid, Water, None }

public sealed record OccupancyCell(Vec3i Offset, OccupancyKind Kind);

// Bloc de construction : matériau de mur/sol/plafond. Toute forme (escalier, toit…) est un cube plein pour les pièces.
public sealed class BlockMaterialInfo
{
    public required string Name { get; init; }
    public int Tier { get; init; }
    public bool IsWall { get; init; } = true;
    public bool IgnoreRooms { get; init; }
    public bool IsRoomMaterialOption { get; init; }
    public bool CountsAsWall => IsWall && !IgnoreRooms;
}

public sealed class HousingValueInfo
{
    public required string Category { get; init; }
    public float BaseValue { get; init; }
    public string TypeForRoomLimit { get; init; } = "";
    public float DiminishingReturnMultiplier { get; init; } = 1f;
    public float DiminishingMultiplierAcrossFullProperty { get; init; } = 1f;
}

public sealed class RoomRequirementInfo
{
    public float? MaterialTier { get; init; }
    public int Volume { get; init; }
    public bool RequiresContainment { get; init; }
    public bool Any => MaterialTier is not null || Volume > 0 || RequiresContainment;
}

public sealed class WorldObjectInfo
{
    public required string Name { get; init; }
    public IReadOnlyList<OccupancyCell> Cells { get; init; } = [];
    public bool IsDefaultOccupancy { get; init; }
    public int? Tier { get; init; }                      // HasTier ⇒ compte dans le tier de pièce quand ses cellules sont des murs (portes)
    public bool HasTableSurface { get; init; }
    public bool CanBeOnSurface { get; init; }
    public string? AttachedSide { get; init; }           // DirectionAxisFlags (« Down », « Up », « Down, Back »…)
    public bool MustBeGridAligned { get; init; }
    public bool WallMounted { get; init; }
    public HousingValueInfo? Housing { get; init; }
    public RoomRequirementInfo? Requirements { get; init; }
    public bool IsCraftingTable { get; init; }

    public bool HasWallCells => Cells.Any(c => c.Kind == OccupancyKind.Wall);
    public bool RequiresSupportBelow => AttachedSide is not null && AttachedSide.Contains("Down", StringComparison.OrdinalIgnoreCase);
    public IEnumerable<OccupancyCell> PlacedCells => Cells.Where(c => c.Kind is OccupancyKind.Occupied or OccupancyKind.Wall or OccupancyKind.Solid);
}

// Constantes et RoomConfig du serveur (bloc « Building » de l'export v5).
public sealed class BuildRules
{
    public int MaxRoomDistance { get; init; } = 70;
    public int MinRoomVolume { get; init; } = 3;
    public int MaxBlockTier { get; init; } = 5;
    public bool EmptyBlocksCountAsWindows { get; init; }
    public int WallBlocksPerWindow { get; init; } = 10;
    public float PaintedBlockTierBonus { get; init; }
    public float PaintedBlockHousingBonus { get; init; } = 0.2f;
    public float RoomCategoryDiminishingReturnRate { get; init; } = 0.1f;
    public float[] HousePointsMultiplierPerResidentsCount { get; init; } = [1f];
    public bool PollutionPenaltyEnabled { get; init; }

    public static BuildRules Vanilla() => new();
}

public sealed class RoomCategoryInfo
{
    public required string Name { get; init; }
    public string? Color { get; init; }
    public string[] SupportingRoomCategoryNames { get; init; } = [];
    public string[] AffectsPropertyTypes { get; init; } = ["Residence", "Cultural"];
    public float MaxSupportPercentOfPrimary { get; init; } = 1f;
    public Dictionary<string, float>? MaxSupportPercentOfPrimaryPerCategory { get; init; }
    public float CapToPercentOfRestOfProperty { get; init; }
    public bool CanBeRoomCategory { get; init; } = true;
    public bool SupportForAnyRoomType { get; init; }
    public bool ShouldCapFromRoomMaterials { get; init; } = true;
    public bool CanAutoChooseCategory { get; init; } = true;
    public bool NegatesValue { get; init; }

    public float GetMaxSupportPercentOfPrimary(RoomCategoryInfo primary)
        => MaxSupportPercentOfPrimaryPerCategory is not null && MaxSupportPercentOfPrimaryPerCategory.TryGetValue(primary.Name, out var v) ? v : MaxSupportPercentOfPrimary;

    public bool IsSupportedBy(RoomCategoryInfo other) => other.SupportForAnyRoomType || SupportingRoomCategoryNames.Contains(other.Name);
}

public sealed class RoomTierInfo
{
    public float TierVal { get; init; }
    public float SoftCap { get; init; }
    public float HardCap { get; init; }
    public float DiminishingReturnPercent { get; init; }
}

// Catégories, caps par tier, multiplicateurs d'occupation (bloc « HousingConfig » de l'export v5).
public sealed class HousingRules
{
    public const string UncategorizedName = "Uncategorized";

    public List<RoomCategoryInfo> Categories { get; init; } = [];
    public List<RoomTierInfo> RoomTiers { get; init; } = [];
    public float[] OccupancyMultipliers { get; init; } = [];
    public bool IsDefault { get; init; }

    public RoomCategoryInfo? GetCategory(string? name) => name is null ? null : Categories.FirstOrDefault(c => c.Name == name);

    public RoomCategoryInfo Uncategorized => GetCategory(UncategorizedName) ?? new RoomCategoryInfo { Name = UncategorizedName, Color = "D3D3D3" };

    public RoomTierInfo GetRoomTier(int tier)
    {
        if (RoomTiers.Count == 0) return new RoomTierInfo { TierVal = tier, SoftCap = float.MaxValue, HardCap = float.MaxValue, DiminishingReturnPercent = 1f };
        var index = Math.Clamp(tier, 0, RoomTiers.Count - 1);
        return RoomTiers[index];
    }

    // OccupancyMultiplierGenerator(n) : 1 pour n ≤ 1, sinon (1/n) × table[n] ; la table exportée est déjà évaluée.
    public float GetOccupancyMultiplier(int residents, BuildRules build)
    {
        if (residents <= 1) return 1f;
        if (OccupancyMultipliers.Length > 0) return OccupancyMultipliers[Math.Clamp(residents, 0, OccupancyMultipliers.Length - 1)];
        var table = build.HousePointsMultiplierPerResidentsCount;
        var crowding = table.Length == 0 ? 1f : table[Math.Clamp(residents, 0, table.Length - 1)];
        return 1f / residents * crowding;
    }

    // Copie de Mods/__core__/Systems/HousingValues.cs (Eco 0.14), utilisée quand le serveur n'a pas exporté sa config.
    public static HousingRules Vanilla() => new()
    {
        IsDefault = true,
        Categories =
        [
            new RoomCategoryInfo { Name = "Living Room", Color = "DB48C5", AffectsPropertyTypes = ["Residence"], SupportingRoomCategoryNames = ["Seating", "Cultural"], MaxSupportPercentOfPrimary = .25f },
            new RoomCategoryInfo { Name = "Bedroom", Color = "00B4A5", AffectsPropertyTypes = ["Residence"], SupportingRoomCategoryNames = ["Living Room", "Seating"] },
            new RoomCategoryInfo { Name = "Kitchen", Color = "4C7BD9", AffectsPropertyTypes = ["Residence"], SupportingRoomCategoryNames = ["Seating"] },
            new RoomCategoryInfo { Name = "Bathroom", Color = "A6E1EA", SupportingRoomCategoryNames = ["Seating"], CapToPercentOfRestOfProperty = .33f },
            new RoomCategoryInfo { Name = "Outdoor", Color = "68E897", SupportingRoomCategoryNames = ["Seating", "Cultural"], CapToPercentOfRestOfProperty = 1f, ShouldCapFromRoomMaterials = false, CanAutoChooseCategory = false },
            new RoomCategoryInfo { Name = "Cultural", Color = "E6B44C", AffectsPropertyTypes = ["Cultural"], MaxSupportPercentOfPrimary = .2f, MaxSupportPercentOfPrimaryPerCategory = new() { { "Outdoor", 1f } }, SupportingRoomCategoryNames = ["Seating"] },
            new RoomCategoryInfo { Name = "Industrial", Color = "A300B4", NegatesValue = true },
            new RoomCategoryInfo { Name = "Seating", Color = "E5956E", CanBeRoomCategory = false, MaxSupportPercentOfPrimary = .3f },
            new RoomCategoryInfo { Name = "Decoration", Color = "6BD6B4", CanBeRoomCategory = false, SupportForAnyRoomType = true, MaxSupportPercentOfPrimary = .5f },
            new RoomCategoryInfo { Name = "Lighting", Color = "FFD6B4", CanBeRoomCategory = false, SupportForAnyRoomType = true, MaxSupportPercentOfPrimary = .5f },
            new RoomCategoryInfo { Name = UncategorizedName, Color = "D3D3D3" },
        ],
        RoomTiers =
        [
            new RoomTierInfo { TierVal = 0, SoftCap = 2f, HardCap = 4f, DiminishingReturnPercent = .65f },
            new RoomTierInfo { TierVal = 1, SoftCap = 5f, HardCap = 10f, DiminishingReturnPercent = .65f },
            new RoomTierInfo { TierVal = 2, SoftCap = 10f, HardCap = 20f, DiminishingReturnPercent = .65f },
            new RoomTierInfo { TierVal = 3, SoftCap = 15f, HardCap = 30f, DiminishingReturnPercent = .65f },
            new RoomTierInfo { TierVal = 4, SoftCap = 20f, HardCap = 40f, DiminishingReturnPercent = .65f },
            new RoomTierInfo { TierVal = 5, SoftCap = 25f, HardCap = 50f, DiminishingReturnPercent = .65f },
        ],
        OccupancyMultipliers = [],
    };
}

// Tout ce que le moteur connaît du serveur : matériaux, objets, règles, bumps de tier des modules installés.
public sealed class Catalog
{
    public Dictionary<string, BlockMaterialInfo> Materials { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, WorldObjectInfo> Objects { get; init; } = new(StringComparer.Ordinal);
    public BuildRules Build { get; init; } = BuildRules.Vanilla();
    public HousingRules Housing { get; init; } = HousingRules.Vanilla();
    public Dictionary<string, float> ModuleTierBumpByTable { get; init; } = new(StringComparer.Ordinal);  // type de table → Σ MaterialTierBump des modules installés

    public BlockMaterialInfo? GetMaterial(string? name) => name is not null && Materials.TryGetValue(name, out var m) ? m : null;
    public WorldObjectInfo? GetObject(string? name) => name is not null && Objects.TryGetValue(name, out var o) ? o : null;
}
