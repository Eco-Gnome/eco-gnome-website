using System.Text.Json;
using System.Text.Json.Serialization;
using ecocraft.Models;

namespace ecocraft.Services.ImportData;

public partial class ImportDataService
{
    private class ImportDataDto
    {
        public required int Version { get; init; }
        public required List<SkillDto> Skills { get; init; } = [];
        public required List<ItemDto> Items { get; init; } = [];
        public required List<TagDto> Tags { get; init; } = [];
        public required List<RecipeDto> Recipes { get; init; } = [];
        public List<ModuleSlotDto> ModuleSlots { get; init; } = [];
        // v5 : conservés bruts, relus par ecocraft.BuildingPlanner.
        public JsonElement? Building { get; init; }
        public JsonElement? HousingConfig { get; init; }
    }

    // v5 : emprise et drapeaux de placement d'un objet posable (Items[].WorldObject).
    private class WorldObjectDto
    {
        public List<OccupancyCellDto> Occupancy { get; init; } = [];
        public bool OccupancyIsDefault { get; init; }
        public int? Tier { get; init; }
        public bool HasTableSurface { get; init; }
        public bool CanBeOnSurface { get; init; }
        public string? RequiredAttachedSide { get; init; }
        public bool MustBeGridAligned { get; init; }
        public bool WallMounted { get; init; }
    }

    private class OccupancyCellDto
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Z { get; init; }
        public string BlockType { get; init; } = "Occupied";
    }

    // v5 : drapeaux du bloc posé par un item de construction (Items[].BuildingBlock).
    private class BuildingBlockDto
    {
        public int Tier { get; init; }
        public bool HasTier { get; init; }
        public bool IsWall { get; init; }
        public bool IsSolid { get; init; }
        public bool IgnoreRooms { get; init; }
        public bool HasForms { get; init; }
        public bool IsRoomMaterialOption { get; init; }
    }

    private class ModuleSlotDto : EcoItemDto
    {
        public int SortOrder { get; init; }
    }

    private class EcoItemDto
    {
        public required string Name { get; set; }
        public required Dictionary<LanguageCode, string> LocalizedName { get; init; }
    }

    private class SkillDto : EcoItemDto
    {
        public string? Profession { get; init; }
        public required int MaxLevel { get; init; }
        public required decimal[] LaborReducePercent { get; init; }
        public required List<TalentDto> Talents { get; init; }
    }

    private class TalentDto : EcoItemDto
    {
        public required Dictionary<LanguageCode, string> LocalizedDescription { get; init; }
        public required string TalentGroupName { get; init; }
        public required int Level { get; init; }
        public required int MaxLevel { get; init; }
        public List<TalentBonusDto> Bonuses { get; init; } = [];
    }

    // Shared bonus shape (v4): used by both Skills[].Talents[].Bonuses and Items[].ModuleBonuses.
    private class TalentBonusDto
    {
        public required TalentBonusAction Action { get; init; }
        public required TalentBonusEffectType EffectType { get; init; }
        public required decimal Value { get; init; }
        public decimal? Cap { get; init; }
        public decimal? Chance { get; init; }
        public decimal[]? Levels { get; init; }
        public string[]? SkillTypes { get; init; }
        public string[]? ExcludedSkillTypes { get; init; }
        public string[]? ItemTags { get; init; }
    }

    private class ItemDto : EcoItemDto
    {
        public bool? IsPluginModule { get; set; }
        public string? ModuleSlot { get; set; }
        public List<TalentBonusDto>? ModuleBonuses { get; set; }
        public decimal? ModuleMaterialTierBump { get; set; }
        public bool? IsCraftingTable { get; set; }
        public List<string>? CraftingTableModuleSlots { get; set; }
        public List<string>? CraftingTablePluginModules { get; set; }
        public RoomRequirementsDto? RoomRequirements { get; set; }
        public decimal? FuelCalories { get; set; }
        public decimal? FuelConsumptionPerSecond { get; set; }
        public string[]? AcceptedFuelTags { get; set; }
        public FoodDto? Food { get; set; }
        public HousingDto? Housing { get; set; }
        public WorldObjectDto? WorldObject { get; set; }
        public BuildingBlockDto? BuildingBlock { get; set; }
    }

    private class RoomRequirementsDto
    {
        public decimal? MaterialTier { get; set; }
        public decimal? Volume { get; set; }
        public bool RequiresContainment { get; set; }
    }

    private class FoodDto
    {
        public decimal Calories { get; set; }
        public decimal Carbs { get; set; }
        public decimal Protein { get; set; }
        public decimal Fat { get; set; }
        public decimal Vitamins { get; set; }
    }

    private class HousingDto
    {
        public string? RoomCategory { get; set; }
        public decimal BaseValue { get; set; }
        public string? TypeForRoomLimit { get; set; }
        public decimal DiminishingReturnMultiplier { get; set; }
        public decimal DiminishingMultiplierAcrossFullProperty { get; set; }
    }

    private class TagDto : EcoItemDto
    {
        public required List<string> AssociatedItems { get; init; }
    }

    private class RecipeDto : EcoItemDto
    {
        public required string FamilyName { get; init; }
        public required DynamicValueDto CraftMinutes { get; init; }
        public required string RequiredSkill { get; init; }
        public required int RequiredSkillLevel { get; init; }
        public required bool IsBlueprint { get; init; }
        public required bool IsDefault { get; init; }
        public required DynamicValueDto Labor { get; init; }
        public required string CraftingTable { get; init; }
        public required List<ElementDto> Ingredients { get; init; }
        public required List<ElementDto> Products { get; init; }
    }

    private class DynamicValueDto
    {
        public required decimal BaseValue { get; set; }
        public required List<ModifierDto> Modifiers { get; init; }
    }

    private class ModifierDto
    {
        public required string DynamicType { get; init; }
        public required string Item { get; init; }
        public string ValueType { get; init; } = "";
    }

    private class ElementDto
    {
        public required string ItemOrTag { get; init; }
        public required DynamicValueDto Quantity { get; init; }

        // ! This is not in the json file, it's calculated after
        public int Index { get; set; }
    }

    private async Task<ImportDataDto> GetServerDataAsDto(EcoCraftDbContext context, Server server)
    {
        var serverWithData = await serverDbService.GetServerWithData(server.Id, context);

        return new ImportDataDto
        {
            Version = ExportVersion,
            Skills = serverWithData.Skills.Select(SkillToDto).ToList(),
            Items = serverWithData.ItemOrTags.Where(iot => !iot.IsTag).Select(s => ItemToDto(s, serverWithData.CraftingTables, serverWithData.PluginModules)).ToList(),
            Tags = serverWithData.ItemOrTags.Where(iot => iot.IsTag).Select(TagToDto).ToList(),
            Recipes = serverWithData.Recipes.Select(RecipeToDto).ToList(),
            ModuleSlots = serverWithData.ModuleSlots.Select(ModuleSlotToDto).ToList(),
            Building = ParseStoredJson(serverWithData.HasBuildingData ? serverWithData.BuildingConfigJson : null),
            HousingConfig = ParseStoredJson(serverWithData.HasBuildingData ? serverWithData.HousingConfigJson : null),
        };
    }

    private static JsonElement? ParseStoredJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static WorldObjectDto? WorldObjectToDto(ItemOrTag item)
    {
        if (item.WorldObjectOccupancyJson is null) return null;

        return new WorldObjectDto
        {
            Occupancy = OccupancyJsonToDto(item.WorldObjectOccupancyJson),
            OccupancyIsDefault = item.WorldObjectOccupancyIsDefault,
            Tier = item.WorldObjectTier,
            HasTableSurface = item.WorldObjectHasTableSurface,
            CanBeOnSurface = item.WorldObjectCanBeOnSurface,
            RequiredAttachedSide = item.WorldObjectAttachedSide,
            MustBeGridAligned = item.WorldObjectMustBeGridAligned,
            WallMounted = item.WorldObjectWallMounted,
        };
    }

    private static BuildingBlockDto? BuildingBlockToDto(ItemOrTag item)
    {
        if (item.BlockIsWall is null) return null;

        return new BuildingBlockDto
        {
            Tier = item.BlockTier ?? 0,
            HasTier = item.BlockTier > 0,
            IsWall = item.BlockIsWall ?? false,
            IsSolid = item.BlockIsWall ?? false,
            IgnoreRooms = item.BlockIgnoreRooms ?? false,
            HasForms = item.BlockHasForms ?? false,
            IsRoomMaterialOption = item.BlockIsRoomMaterialOption ?? false,
        };
    }

    // Stockage compact des cellules d'occupation : [{"x":0,"y":0,"z":0,"k":"O"}], k = O|W|S|L|N.
    private static string OccupancyDtoToJson(List<OccupancyCellDto> cells)
    {
        var compact = cells.Select(c => new OccupancyCellStorage
        {
            X = c.X,
            Y = c.Y,
            Z = c.Z,
            K = c.BlockType switch
            {
                "Wall" => "W",
                "Solid" => "S",
                "Water" => "L",
                "None" => "N",
                _ => "O",
            },
        });

        return JsonSerializer.Serialize(compact);
    }

    private static List<OccupancyCellDto> OccupancyJsonToDto(string json)
    {
        var cells = JsonSerializer.Deserialize<List<OccupancyCellStorage>>(json) ?? [];

        return cells.Select(c => new OccupancyCellDto
        {
            X = c.X,
            Y = c.Y,
            Z = c.Z,
            BlockType = c.K switch
            {
                "W" => "Wall",
                "S" => "Solid",
                "L" => "Water",
                "N" => "None",
                _ => "Occupied",
            },
        }).ToList();
    }

    private class OccupancyCellStorage
    {
        [JsonPropertyName("x")] public int X { get; init; }
        [JsonPropertyName("y")] public int Y { get; init; }
        [JsonPropertyName("z")] public int Z { get; init; }
        [JsonPropertyName("k")] public string K { get; init; } = "O";
    }

    private static ModuleSlotDto ModuleSlotToDto(ModuleSlot moduleSlot)
    {
        return new ModuleSlotDto
        {
            Name = moduleSlot.Name,
            LocalizedName = LocalizedFieldToDto(moduleSlot.LocalizedName),
            SortOrder = moduleSlot.SortOrder,
        };
    }

    private static SkillDto SkillToDto(Skill skill)
    {
        return new SkillDto
        {
            Name = skill.Name,
            LocalizedName = LocalizedFieldToDto(skill.LocalizedName),
            Profession = skill.Profession,
            LaborReducePercent = skill.LaborReducePercent,
            MaxLevel = skill.MaxLevel,
            Talents = skill.Talents.Select(TalentToDto).ToList(),
        };
    }

    private static TalentDto TalentToDto(Talent talent)
    {
        return new TalentDto
        {
            Name = talent.Name,
            LocalizedName = LocalizedFieldToDto(talent.LocalizedName),
            LocalizedDescription = LocalizedFieldToDto(talent.LocalizedDescription),
            TalentGroupName = talent.TalentGroupName,
            Level = talent.Level,
            MaxLevel = talent.MaxLevel,
            Bonuses = talent.Bonuses.Select(TalentBonusToDto).ToList(),
        };
    }

    private static TalentBonusDto TalentBonusToDto(TalentBonus bonus)
    {
        return new TalentBonusDto
        {
            Action = bonus.Action,
            EffectType = bonus.EffectType,
            Value = bonus.Value,
            Cap = bonus.Cap,
            Chance = bonus.Chance,
            Levels = bonus.Levels,
            SkillTypes = bonus.SkillTypes,
            ExcludedSkillTypes = bonus.ExcludedSkillTypes,
            ItemTags = bonus.ItemTags,
        };
    }

    private static ItemDto ItemToDto(ItemOrTag item, List<CraftingTable> craftingTables, List<PluginModule> pluginModules)
    {
        var itemDto = new ItemDto
        {
            Name = item.Name,
            LocalizedName = LocalizedFieldToDto(item.LocalizedName),
            IsCraftingTable = false,
            IsPluginModule = false,
            FuelCalories = item.FuelCalories,
            FuelConsumptionPerSecond = item.FuelConsumptionPerSecond,
            AcceptedFuelTags = item.AcceptedFuelTags,
            Food = item.FoodCalories is null ? null : new FoodDto
            {
                Calories = item.FoodCalories.Value,
                Carbs = item.FoodCarbs ?? 0,
                Protein = item.FoodProtein ?? 0,
                Fat = item.FoodFat ?? 0,
                Vitamins = item.FoodVitamins ?? 0,
            },
            Housing = item.HousingBaseValue is null ? null : new HousingDto
            {
                RoomCategory = item.HousingRoomCategory,
                BaseValue = item.HousingBaseValue.Value,
                TypeForRoomLimit = item.HousingTypeForRoomLimit,
                DiminishingReturnMultiplier = item.HousingDiminishingReturnMultiplier ?? 1,
                DiminishingMultiplierAcrossFullProperty = item.HousingDiminishingMultiplierAcrossFullProperty ?? 1,
            },
            RoomRequirements = item.RoomMaterialTier is null && item.RoomVolume is null && !item.RoomRequiresContainment ? null : new RoomRequirementsDto
            {
                MaterialTier = item.RoomMaterialTier,
                Volume = item.RoomVolume,
                RequiresContainment = item.RoomRequiresContainment,
            },
            WorldObject = WorldObjectToDto(item),
            BuildingBlock = BuildingBlockToDto(item),
        };

        var associatedCraftingTable = craftingTables.FirstOrDefault(c => c.Name == item.Name);
        if (associatedCraftingTable is not null)
        {
            itemDto.IsCraftingTable = true;
            itemDto.CraftingTableModuleSlots = associatedCraftingTable.ModuleSlots.Select(ms => ms.Name).ToList();
            itemDto.CraftingTablePluginModules = associatedCraftingTable.PluginModules.Select(p => p.Name).ToList();

            return itemDto;
        }

        var associatedPluginModule = pluginModules.FirstOrDefault(c => c.Name == item.Name);
        if (associatedPluginModule is not null)
        {
            itemDto.IsPluginModule = true;
            itemDto.ModuleSlot = associatedPluginModule.ModuleSlot?.Name;
            itemDto.ModuleBonuses = associatedPluginModule.Bonuses.Select(TalentBonusToDto).ToList();
            itemDto.ModuleMaterialTierBump = associatedPluginModule.MaterialTierBump;
        }

        return itemDto;
    }

    private static TagDto TagToDto(ItemOrTag tag)
    {
        return new TagDto
        {
            Name = tag.Name,
            LocalizedName = LocalizedFieldToDto(tag.LocalizedName),
            AssociatedItems = tag.AssociatedItems.Select(i => i.Name).ToList(),
        };
    }

    private static RecipeDto RecipeToDto(Recipe recipe)
    {
        var recipes = new RecipeDto
        {
            Name = recipe.Name,
            LocalizedName = LocalizedFieldToDto(recipe.LocalizedName),
            Ingredients = recipe.Elements.Where(e => e.IsIngredient()).OrderBy(e => e.Index).Select(ElementToDto).ToList(),
            Products = recipe.Elements.Where(e => e.IsProduct()).OrderBy(e => e.Index).Select(ElementToDto).ToList(),
            Labor = DynamicValueToDto(recipe.Labor),
            CraftingTable = recipe.CraftingTable.Name,
            CraftMinutes = DynamicValueToDto(recipe.CraftMinutes),
            FamilyName = recipe.FamilyName,
            IsBlueprint = recipe.IsBlueprint,
            IsDefault = recipe.IsDefault,
            RequiredSkill = recipe.Skill?.Name ?? "",
            RequiredSkillLevel = (int)recipe.SkillLevel,
        };

        recipes.Ingredients.ForEach(i => i.Quantity.BaseValue *= -1);

        return recipes;
    }

    private static DynamicValueDto DynamicValueToDto(DynamicValue dynamicValue)
    {
        return new DynamicValueDto
        {
            BaseValue = dynamicValue.BaseValue,
            Modifiers = dynamicValue.Modifiers.Select(ModifierToDto).ToList(),
        };
    }

    private static ModifierDto ModifierToDto(Modifier modifier)
    {
        return new ModifierDto
        {
            Item = modifier.Skill?.Name ?? modifier.Talent?.Name ?? "",
            DynamicType = modifier.DynamicType,
            ValueType = modifier.ValueType,
        };
    }

    private static ElementDto ElementToDto(Element element)
    {
        return new ElementDto
        {
            ItemOrTag = element.ItemOrTag.Name,
            Quantity = DynamicValueToDto(element.Quantity),
        };
    }

    private static Dictionary<LanguageCode, string> LocalizedFieldToDto(LocalizedField localizedField)
    {
        var result = new Dictionary<LanguageCode, string>
        {
            { LanguageCode.en_US, localizedField.en_US },
            { LanguageCode.fr, localizedField.fr },
            { LanguageCode.es, localizedField.es },
            { LanguageCode.de, localizedField.de },
            { LanguageCode.ko, localizedField.ko },
            { LanguageCode.pt_BR, localizedField.pt_BR },
            { LanguageCode.zh_Hans, localizedField.zh_Hans },
            { LanguageCode.ru, localizedField.ru },
            { LanguageCode.it, localizedField.it },
            { LanguageCode.pt_PT, localizedField.pt_PT },
            { LanguageCode.hu, localizedField.hu },
            { LanguageCode.ja, localizedField.ja },
            { LanguageCode.nn, localizedField.nn },
            { LanguageCode.pl, localizedField.pl },
            { LanguageCode.nl, localizedField.nl },
            { LanguageCode.ro, localizedField.ro },
            { LanguageCode.da, localizedField.da },
            { LanguageCode.cs, localizedField.cs },
            { LanguageCode.sv, localizedField.sv },
            { LanguageCode.uk, localizedField.uk },
            { LanguageCode.el, localizedField.el },
            { LanguageCode.ar_sa, localizedField.ar_sa },
            { LanguageCode.vi, localizedField.vi },
            { LanguageCode.tr, localizedField.tr }
        };

        return result;
    }
}
