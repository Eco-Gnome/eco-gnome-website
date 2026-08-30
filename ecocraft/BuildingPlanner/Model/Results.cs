namespace ecocraft.BuildingPlanner.Model;

public enum IssueSeverity { Info, Warning, Error }

// Message = clé de traduction « BuildingPlanner.Issue.{Code} », Args = valeurs des {placeholders}.
// Level = niveau concerné (null → tous / non localisé) ; l'analyseur le déduit de RoomId/ObjectId s'il manque.
public sealed record PlanIssue(IssueSeverity Severity, string Code, string[] Args, GridPoint? Cell = null, string? ObjectId = null, string? RoomId = null, int? Level = null)
{
    public static PlanIssue Error(string code, string[] args, GridPoint? cell = null, string? objectId = null, string? roomId = null, int? level = null) => new(IssueSeverity.Error, code, args, cell, objectId, roomId, level);
    public static PlanIssue Warning(string code, string[] args, GridPoint? cell = null, string? objectId = null, string? roomId = null, int? level = null) => new(IssueSeverity.Warning, code, args, cell, objectId, roomId, level);
    public static PlanIssue Info(string code, string[] args, GridPoint? cell = null, string? objectId = null, string? roomId = null, int? level = null) => new(IssueSeverity.Info, code, args, cell, objectId, roomId, level);
}

public sealed class PlacedObjectResult
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public bool Known { get; init; }
    public bool Placed { get; init; }
    public Vec3i Origin { get; init; }                 // coordonnées Eco (X, Y vertical, Z)
    public int Rotation { get; init; }
    public List<Vec3i> Cells { get; init; } = [];      // cellules posées (monde)
    public string? AttachedTo { get; init; }
    public string? RoomId { get; set; }
    public bool IsDoor { get; init; }
}

public sealed class RoomAnalysis
{
    public required string RoomId { get; init; }
    public required string Name { get; init; }
    public bool Contained { get; set; }
    public string? FailCode { get; set; }              // RoomSeedInWall | RoomTooBig | NoCeiling | VolumeTooSmall
    public GridPoint? FailCell { get; set; }
    public int? FailHeight { get; set; }               // relatif au niveau de la cellule d'échec
    public int? FailLevel { get; set; }
    public int Volume { get; set; }
    public int WallCount { get; set; }
    public Dictionary<float, int> WallTierComposition { get; set; } = new();
    public float AverageTier { get; set; }
    public int EmptyEdgeCount { get; set; }
    public float AverageTierWithoutEmptyEdges { get; set; }
    public int FootprintCellCount { get; set; }
    public int Height { get; set; }
    public List<string> ObjectIds { get; set; } = [];
    public List<TableCheck> Tables { get; set; } = [];
    public RoomHousingResult? Housing { get; set; }
    public int RequiredVolumeTotal { get; set; }
}

public sealed class TableCheck
{
    public required string ObjectId { get; init; }
    public required string Type { get; init; }
    public string? RoomId { get; init; }
    public float? BaseTier { get; init; }
    public float ModuleBump { get; init; }
    public float? EffectiveTier { get; init; }
    public float RoomTier { get; init; }
    public bool TierOk { get; init; }
    public bool ModulesOk { get; init; }               // false : la table fonctionne mais ses modules sont inactifs
    public float TierGap { get; init; }
    public int RequiredVolume { get; init; }
    public int RoomVolume { get; init; }
    public int RoomVolumeUsed { get; init; }
    public bool VolumeOk { get; init; }
    public int VolumeGap { get; init; }
    public bool RequiresContainment { get; init; }
    public bool ContainmentOk { get; init; }
    public bool InRoom { get; init; }
    public bool Satisfied => InRoom && TierOk && VolumeOk && ContainmentOk;
}

public sealed class ObjectHousingLine
{
    public required string ObjectId { get; init; }
    public required string Type { get; init; }
    public required string Category { get; init; }
    public string TypeForRoomLimit { get; init; } = "";
    public float FurnishingValue { get; init; }         // valeur de base × pénalité propriété
    public float Multiplier { get; init; }              // rendement décroissant dans la pièce
    public float Value { get; init; }
    public bool Excluded { get; init; }
}

public sealed class RoomHousingResult
{
    public required string RoomId { get; init; }
    public string PrimaryCategory { get; set; } = "Uncategorized";
    public bool CategoryLocked { get; set; }
    public bool Negated { get; set; }
    public bool PrimaryTie { get; set; }
    public float PrimaryValue { get; set; }
    public Dictionary<string, float> ValueByCategory { get; set; } = new();     // après plafond de support et arrondi
    public Dictionary<string, float> RawValueByCategory { get; set; } = new();  // avant plafond
    public float TotalBeforeCap { get; set; }
    public float Value { get; set; }                    // après cap de tier
    public float TierSoftCap { get; set; }
    public float TierHardCap { get; set; }
    public float TierVal { get; set; }
    public bool CappedByTier { get; set; }
    public List<ObjectHousingLine> Objects { get; set; } = [];
}

public sealed class RoomContribution
{
    public required string RoomId { get; init; }
    public required string Category { get; init; }
    public float RoomValue { get; init; }
    public float Multiplier { get; init; }
    public float Contribution { get; init; }
    public int Rank { get; init; }
}

public sealed class PropertyHousingResult
{
    public int Residents { get; init; }
    public float? Target { get; init; }
    public float Total { get; set; }
    public float TotalBeforeOccupancy { get; set; }
    public float OccupancyMultiplier { get; set; } = 1f;
    public Dictionary<string, float> ByCategory { get; set; } = new();
    public Dictionary<string, float> CapAppliedByCategory { get; set; } = new();  // catégorie → plafond appliqué (valeur avant)
    public float UncappedTotal { get; set; }                                       // somme des pièces principales (base des plafonds)
    public List<RoomContribution> Rooms { get; set; } = [];
    public bool TargetReached => Target is null || Total >= Target.Value;
}

public sealed class MaterialCostLine
{
    public required string Material { get; init; }
    public int Walls { get; set; }
    public int Floors { get; set; }
    public int Ceilings { get; set; }
    public int Total => Walls + Floors + Ceilings;
}

public sealed class ObjectCostLine
{
    public required string Type { get; init; }
    public int Count { get; set; }
}

public sealed class AnalysisResult
{
    public List<PlanIssue> Issues { get; init; } = [];
    public bool Blocked { get; init; }                  // validation en échec : rien d'autre n'est calculé
    public List<RoomAnalysis> Rooms { get; init; } = [];
    public List<TableCheck> Tables { get; init; } = [];
    public PropertyHousingResult? Housing { get; init; }
    public List<MaterialCostLine> Materials { get; init; } = [];
    public List<ObjectCostLine> ObjectCounts { get; init; } = [];
    public List<PlacedObjectResult> Objects { get; init; } = [];
    public int GridSizeY { get; init; }
    public bool HousingRulesAreDefaults { get; init; }
}
