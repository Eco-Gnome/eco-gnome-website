using System.Text.Json;
using System.Text.Json.Serialization;

namespace ecocraft.BuildingPlanner.Model;

// Relit les blocs bruts « Building » et « HousingConfig » de l'export v5 (PascalCase) stockés sur le serveur.
public static class CatalogJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static BuildRules ParseBuildRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return BuildRules.Vanilla();
        try
        {
            var dto = JsonSerializer.Deserialize<BuildingDto>(json, Options);
            if (dto is null) return BuildRules.Vanilla();
            var config = dto.RoomConfig ?? new RoomConfigDto();
            return new BuildRules
            {
                MaxRoomDistance = dto.MaxRoomDistance ?? 70,
                MinRoomVolume = dto.MinRoomVolume ?? 3,
                MaxBlockTier = dto.MaxBlockTier ?? 5,
                EmptyBlocksCountAsWindows = config.EmptyBlocksCountAsWindows ?? false,
                WallBlocksPerWindow = config.WallBlocksPerWindow ?? 10,
                PaintedBlockTierBonus = config.PaintedBlockTierBonus ?? 0f,
                PaintedBlockHousingBonus = config.PaintedBlockHousingBonus ?? 0.2f,
                RoomCategoryDiminishingReturnRate = config.RoomCategoryDiminishingReturnRate ?? 0.1f,
                HousePointsMultiplierPerResidentsCount = config.HousePointsMultiplierPerResidentsCount is { Length: > 0 } t ? t : [1f],
                PollutionPenaltyEnabled = config.PollutionPenaltyEnabled ?? false,
            };
        }
        catch (JsonException)
        {
            return BuildRules.Vanilla();
        }
    }

    public static HousingRules ParseHousingRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return HousingRules.Vanilla();
        try
        {
            var dto = JsonSerializer.Deserialize<HousingConfigDto>(json, Options);
            if (dto is null || dto.Categories is not { Count: > 0 } || dto.RoomTiers is not { Count: > 0 }) return HousingRules.Vanilla();

            var categories = dto.Categories.Select(c => new RoomCategoryInfo
            {
                Name = c.Name ?? "",
                Color = c.Color,
                SupportingRoomCategoryNames = c.SupportingRoomCategoryNames ?? [],
                AffectsPropertyTypes = c.AffectsPropertyTypes ?? ["Residence", "Cultural"],
                MaxSupportPercentOfPrimary = c.MaxSupportPercentOfPrimary ?? 1f,
                MaxSupportPercentOfPrimaryPerCategory = c.MaxSupportPercentOfPrimaryPerCategory,
                CapToPercentOfRestOfProperty = c.CapToPercentOfRestOfProperty ?? 0f,
                CanBeRoomCategory = c.CanBeRoomCategory ?? true,
                SupportForAnyRoomType = c.SupportForAnyRoomType ?? false,
                ShouldCapFromRoomMaterials = c.ShouldCapFromRoomMaterials ?? true,
                CanAutoChooseCategory = c.CanAutoChooseCategory ?? true,
                NegatesValue = c.NegatesValue ?? false,
            }).Where(c => c.Name.Length > 0).ToList();

            if (categories.All(c => c.Name != HousingRules.UncategorizedName))
                categories.Add(new RoomCategoryInfo { Name = HousingRules.UncategorizedName, Color = "D3D3D3" });

            return new HousingRules
            {
                Categories = categories,
                RoomTiers = dto.RoomTiers.Select(t => new RoomTierInfo
                {
                    TierVal = t.TierVal ?? 0f,
                    SoftCap = t.SoftCap ?? 0f,
                    HardCap = t.HardCap ?? 0f,
                    DiminishingReturnPercent = t.DiminishingReturnPercent ?? .65f,
                }).ToList(),
                OccupancyMultipliers = dto.OccupancyMultipliers ?? [],
            };
        }
        catch (JsonException)
        {
            return HousingRules.Vanilla();
        }
    }

    private sealed class BuildingDto
    {
        public int? MaxRoomDistance { get; set; }
        public int? MinRoomVolume { get; set; }
        public int? MaxBlockTier { get; set; }
        public RoomConfigDto? RoomConfig { get; set; }
    }

    private sealed class RoomConfigDto
    {
        public bool? EmptyBlocksCountAsWindows { get; set; }
        public int? WallBlocksPerWindow { get; set; }
        public float? PaintedBlockTierBonus { get; set; }
        public float? PaintedBlockHousingBonus { get; set; }
        public float? RoomCategoryDiminishingReturnRate { get; set; }
        public float[]? HousePointsMultiplierPerResidentsCount { get; set; }
        public bool? PollutionPenaltyEnabled { get; set; }
    }

    private sealed class HousingConfigDto
    {
        public List<RoomCategoryDto>? Categories { get; set; }
        public List<RoomTierDto>? RoomTiers { get; set; }
        public float[]? OccupancyMultipliers { get; set; }
    }

    private sealed class RoomCategoryDto
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string[]? SupportingRoomCategoryNames { get; set; }
        public string[]? AffectsPropertyTypes { get; set; }
        public float? MaxSupportPercentOfPrimary { get; set; }
        public Dictionary<string, float>? MaxSupportPercentOfPrimaryPerCategory { get; set; }
        public float? CapToPercentOfRestOfProperty { get; set; }
        public bool? CanBeRoomCategory { get; set; }
        public bool? SupportForAnyRoomType { get; set; }
        public bool? ShouldCapFromRoomMaterials { get; set; }
        public bool? CanAutoChooseCategory { get; set; }
        public bool? NegatesValue { get; set; }
    }

    private sealed class RoomTierDto
    {
        public float? TierVal { get; set; }
        public float? SoftCap { get; set; }
        public float? HardCap { get; set; }
        public float? DiminishingReturnPercent { get; set; }
    }
}
