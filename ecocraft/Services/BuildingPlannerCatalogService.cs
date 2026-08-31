using System.Text.Json;
using System.Text.Json.Serialization;
using ecocraft.BuildingPlanner;
using ecocraft.BuildingPlanner.Model;
using ecocraft.Models;

namespace ecocraft.Services;

// Construit le catalogue du moteur (matériaux, objets, règles) depuis les données du serveur importées en v5,
// plus les bumps de tier des modules installés dans le DataContext courant. Fournit aussi la version « client »
// envoyée à l'îlot JavaScript (libellés traduits, icônes, cellules d'occupation).
public sealed class BuildingPlannerCatalogService(LocalizationService localizationService)
{
    private Guid? _cachedServerId;
    private DateTimeOffset? _cachedUploadTime;
    private Catalog? _cachedBase;

    public Catalog GetCatalog(Server serverData, DataContext? dataContext)
    {
        var baseCatalog = GetBaseCatalog(serverData);

        var bumps = new Dictionary<string, float>(StringComparer.Ordinal);
        if (dataContext is not null)
        {
            foreach (var uct in dataContext.UserCraftingTables)
            {
                if (uct.CraftingTable is null) continue;
                var bump = uct.PluginModules.Sum(pm => pm.MaterialTierBump ?? 0m);
                if (bump > 0) bumps[uct.CraftingTable.Name] = (float)bump;
            }
        }

        return new Catalog
        {
            Materials = baseCatalog.Materials,
            Objects = baseCatalog.Objects,
            Build = baseCatalog.Build,
            Housing = baseCatalog.Housing,
            ModuleTierBumpByTable = bumps,
        };
    }

    private Catalog GetBaseCatalog(Server serverData)
    {
        if (_cachedBase is not null && _cachedServerId == serverData.Id && _cachedUploadTime == serverData.LastDataUploadTime) return _cachedBase;

        var craftingTableNames = serverData.CraftingTables.Select(ct => ct.Name).ToHashSet(StringComparer.Ordinal);
        var materials = new Dictionary<string, BlockMaterialInfo>(StringComparer.Ordinal);
        var objects = new Dictionary<string, WorldObjectInfo>(StringComparer.Ordinal);

        foreach (var item in serverData.ItemOrTags.Where(i => !i.IsTag))
        {
            if (item.BlockIsWall is not null)
            {
                materials[item.Name] = new BlockMaterialInfo
                {
                    Name = item.Name,
                    Tier = item.BlockTier ?? 0,
                    IsWall = item.BlockIsWall ?? false,
                    IgnoreRooms = item.BlockIgnoreRooms ?? false,
                    IsRoomMaterialOption = item.BlockIsRoomMaterialOption ?? false,
                };
            }

            if (item.WorldObjectOccupancyJson is not null)
            {
                objects[item.Name] = new WorldObjectInfo
                {
                    Name = item.Name,
                    Cells = ParseOccupancy(item.WorldObjectOccupancyJson),
                    IsDefaultOccupancy = item.WorldObjectOccupancyIsDefault,
                    Tier = item.WorldObjectTier,
                    HasTableSurface = item.WorldObjectHasTableSurface,
                    CanBeOnSurface = item.WorldObjectCanBeOnSurface,
                    AttachedSide = item.WorldObjectAttachedSide,
                    MustBeGridAligned = item.WorldObjectMustBeGridAligned,
                    WallMounted = item.WorldObjectWallMounted,
                    IsCraftingTable = craftingTableNames.Contains(item.Name),
                    Housing = item.HousingBaseValue is null && item.HousingRoomCategory is null ? null : new HousingValueInfo
                    {
                        Category = item.HousingRoomCategory ?? HousingRules.UncategorizedName,
                        BaseValue = (float)(item.HousingBaseValue ?? 0m),
                        TypeForRoomLimit = item.HousingTypeForRoomLimit ?? "",
                        DiminishingReturnMultiplier = (float)(item.HousingDiminishingReturnMultiplier ?? 1m),
                        DiminishingMultiplierAcrossFullProperty = (float)(item.HousingDiminishingMultiplierAcrossFullProperty ?? 1m),
                    },
                    Requirements = item.RoomMaterialTier is null && item.RoomVolume is null && !item.RoomRequiresContainment ? null : new RoomRequirementInfo
                    {
                        MaterialTier = item.RoomMaterialTier is { } t ? (float)t : null,
                        Volume = (int)(item.RoomVolume ?? 0m),
                        RequiresContainment = item.RoomRequiresContainment,
                    },
                };
            }
        }

        _cachedBase = new Catalog
        {
            Materials = materials,
            Objects = objects,
            Build = CatalogJson.ParseBuildRules(serverData.BuildingConfigJson),
            Housing = CatalogJson.ParseHousingRules(serverData.HousingConfigJson),
        };
        _cachedServerId = serverData.Id;
        _cachedUploadTime = serverData.LastDataUploadTime;
        return _cachedBase;
    }

    public void InvalidateCache() => _cachedBase = null;

    private static IReadOnlyList<OccupancyCell> ParseOccupancy(string json)
    {
        try
        {
            var cells = JsonSerializer.Deserialize<List<StoredCell>>(json) ?? [];
            return cells.Select(c => new OccupancyCell(new Vec3i(c.X, c.Y, c.Z), c.K switch
            {
                "W" => OccupancyKind.Wall,
                "S" => OccupancyKind.Solid,
                "L" => OccupancyKind.Water,
                "N" => OccupancyKind.None,
                _ => OccupancyKind.Occupied,
            })).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class StoredCell
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("z")] public int Z { get; set; }
        [JsonPropertyName("k")] public string K { get; set; } = "O";
    }

    // Boîte englobante des cellules posées : largeur (X) × profondeur (Z) × hauteur (Y), en cases du plan.
    private static int[] SizeOf(WorldObjectInfo o)
    {
        var cells = o.PlacedCells.ToList();
        if (cells.Count == 0) return [1, 1, 1];
        return
        [
            cells.Max(c => c.Offset.X) - cells.Min(c => c.Offset.X) + 1,
            cells.Max(c => c.Offset.Z) - cells.Min(c => c.Offset.Z) + 1,
            cells.Max(c => c.Offset.Y) - cells.Min(c => c.Offset.Y) + 1,
        ];
    }

    // Catalogue pour l'îlot JS : libellés traduits, icônes, cellules brutes (le JS applique la rotation lui-même).
    public ClientCatalog BuildClientCatalog(Catalog catalog, Server serverData)
    {
        var itemsByName = serverData.ItemOrTags.Where(i => !i.IsTag).ToDictionary(i => i.Name, i => i, StringComparer.Ordinal);
        string Label(string name) => itemsByName.TryGetValue(name, out var item) ? localizationService.GetTranslation(item) : name;

        var materials = catalog.Materials.Values
            .Where(m => m.CountsAsWall && m.Tier >= 1)
            .Select(m => new ClientMaterial
            {
                Name = m.Name,
                Label = Label(m.Name),
                Tier = m.Tier,
                IsRoomMaterialOption = m.IsRoomMaterialOption,
            })
            .OrderBy(m => m.Tier).ThenBy(m => m.Label)
            .ToList();

        // Tout objet avec une empreinte est proposé (stockages, véhicules... → onglet « Autres »), sauf les panneaux
        // décoratifs sans effet pièce/housing (heuristique sur le nom, ~130 variantes en vanilla).
        var objects = catalog.Objects.Values
            .Where(o => o.IsCraftingTable || o.Requirements is not null || o.Housing is not null || o.HasWallCells || o.HasTableSurface || o.Tier is not null || !o.Name.Contains("Sign", StringComparison.Ordinal))
            .Select(o => new ClientObject
            {
                Name = o.Name,
                Label = Label(o.Name),
                Cells = o.Cells.Select(c => new[] { c.Offset.X, c.Offset.Y, c.Offset.Z, (int)c.Kind }).ToList(),
                Size = SizeOf(o),
                IsDoor = o.HasWallCells,
                HasTableSurface = o.HasTableSurface,
                CanBeOnSurface = o.CanBeOnSurface,
                Tier = o.Tier,
                IsCraftingTable = o.IsCraftingTable,
                HousingCategory = o.Housing?.Category,
                HousingValue = o.Housing?.BaseValue,
                RequiredTier = o.Requirements?.MaterialTier,
                RequiredVolume = o.Requirements?.Volume ?? 0,
                RequiresContainment = o.Requirements?.RequiresContainment ?? false,
                IsDefaultOccupancy = o.IsDefaultOccupancy,
                Group = o.IsCraftingTable || (o.Housing is null && o.Requirements?.MaterialTier is not null) ? "table" : o.HasWallCells ? "door" : o.Housing is not null ? "housing" : "other",
            })
            .OrderBy(o => o.Group).ThenBy(o => o.HousingCategory).ThenBy(o => o.Label)
            .ToList();

        var categories = catalog.Housing.Categories.Select(c => new ClientCategory { Name = c.Name, Label = c.Name, Color = c.Color, CapPercent = c.CapToPercentOfRestOfProperty }).ToList();

        return new ClientCatalog
        {
            ServerId = serverData.Id,
            MaxBlockTier = catalog.Build.MaxBlockTier,
            Materials = materials,
            Objects = objects,
            Categories = categories,
            ModuleBumps = catalog.ModuleTierBumpByTable,
        };
    }
}

public sealed class ClientCatalog
{
    public Guid ServerId { get; init; }
    public int MaxBlockTier { get; init; }
    public List<ClientMaterial> Materials { get; init; } = [];
    public List<ClientObject> Objects { get; init; } = [];
    public List<ClientCategory> Categories { get; init; } = [];
    public Dictionary<string, float> ModuleBumps { get; init; } = new();
}

public sealed class ClientMaterial
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public int Tier { get; init; }
    public bool IsRoomMaterialOption { get; init; }
}

public sealed class ClientObject
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public List<int[]> Cells { get; init; } = [];        // [x, y, z, kind] kind : 0 Occupied, 1 Wall, 2 Solid, 3 Water, 4 None
    public int[] Size { get; init; } = [1, 1, 1];        // largeur × profondeur × hauteur
    public bool IsDoor { get; init; }
    public bool HasTableSurface { get; init; }
    public bool CanBeOnSurface { get; init; }
    public int? Tier { get; init; }
    public bool IsCraftingTable { get; init; }
    public string? HousingCategory { get; init; }
    public float? HousingValue { get; init; }
    public float? RequiredTier { get; init; }
    public int RequiredVolume { get; init; }
    public bool RequiresContainment { get; init; }
    public bool IsDefaultOccupancy { get; init; }
    public required string Group { get; init; }
}

public sealed class ClientCategory
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public string? Color { get; init; }
    public float CapPercent { get; init; }               // 0 = pièce principale ; sinon part max des pièces principales
}
