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
        public required decimal Value { get; init; }
        public required int Level { get; init; }
        public required int MaxLevel { get; init; }
    }

    private class ItemDto : EcoItemDto
    {
        public bool? IsPluginModule { get; set; }
        public string? PluginType { get; set; }
        public decimal? PluginModulePercent { get; set; }
        public string? PluginModuleSkill { get; set; }
        public decimal? PluginModuleSkillPercent { get; set; }
        public bool? IsCraftingTable { get; set; }
        public List<string>? CraftingTablePluginModules { get; set; }
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
            Version = SupportedVersion,
            Skills = serverWithData.Skills.Select(SkillToDto).ToList(),
            Items = serverWithData.ItemOrTags.Where(iot => !iot.IsTag).Select(s => ItemToDto(s, serverWithData.CraftingTables, serverWithData.PluginModules)).ToList(),
            Tags = serverWithData.ItemOrTags.Where(iot => iot.IsTag).Select(TagToDto).ToList(),
            Recipes = serverWithData.Recipes.Select(RecipeToDto).ToList(),
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
            Value = talent.Value,
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
        };

        var associatedCraftingTable = craftingTables.FirstOrDefault(c => c.Name == item.Name);
        if (associatedCraftingTable is not null)
        {
            itemDto.IsCraftingTable = true;
            itemDto.CraftingTablePluginModules = associatedCraftingTable.PluginModules.Select(p => p.Name).ToList();

            return itemDto;
        }

        var associatedPluginModule = pluginModules.FirstOrDefault(c => c.Name == item.Name);
        if (associatedPluginModule is not null)
        {
            itemDto.IsPluginModule = true;
            itemDto.PluginType = associatedPluginModule.PluginType switch
            {
                PluginType.Resource => "Resource",
                PluginType.Speed => "Speed",
                PluginType.ResourceAndSpeed => "Resource&Speed",
                _ => null
            };
            itemDto.PluginModulePercent = associatedPluginModule.Percent;
            itemDto.PluginModuleSkill = associatedPluginModule.Skill?.Name;
            itemDto.PluginModuleSkillPercent = associatedPluginModule.SkillPercent;
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
