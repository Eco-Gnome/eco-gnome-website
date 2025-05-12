using System.Text.Json;
using System.Text.Json.Serialization;
using ecocraft.Models;

namespace ecocraft.Services.ImportData;

public class ImportException(string? message) : Exception(message);

public class ImportDataService(
    EcoCraftDbContext dbContext,
    LocalizationService localizationService,
    ServerDataService serverDataService)
{
    private const int SupportedVersion = 1;

    public async Task<(int, string[])> ImportServerData(string jsonContent, Server server)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LanguageCodeDictionaryConverter());

        ImportDataDto? importedData;
        var errorCount = 0;

        try
        {
            importedData = JsonSerializer.Deserialize<ImportDataDto>(jsonContent, options);
        }
        catch (Exception e)
        {
            throw new ImportException("No data / Wrong file format: " + e.Message);
        }

        if (importedData is null) throw new ImportException("No data / Wrong file format");

        if (importedData.Version != SupportedVersion) throw new ImportException(localizationService.GetTranslation("ServerManagement.Snackbar.UploadWrongVersion", SupportedVersion.ToString()));

        ImportSkills(server, importedData.Skills);
        errorCount += ImportItems(server, importedData.Items, out var itemErrorNames);
        ImportTags(server, importedData.Tags);
        errorCount += ImportRecipes(server, importedData.Recipes, out var recipeErrorNames);

        await dbContext.SaveChangesAsync();

        return (errorCount, itemErrorNames.Concat(recipeErrorNames).ToArray());
    }

    public async Task CopyServerData(Server copyServer, Server targetServer)
    {
        var data = await GetServerDataAsDto(copyServer);

        ImportSkills(targetServer, data.Skills);
        ImportItems(targetServer, data.Items, out _);
        ImportTags(targetServer, data.Tags);
        ImportRecipes(targetServer, data.Recipes, out _);

        await dbContext.SaveChangesAsync();
    }

    private async Task<ImportDataDto> GetServerDataAsDto(Server server)
    {
        var serverData = await serverDataService.GetServerData(server);

        return new ImportDataDto()
        {
            Version = SupportedVersion,
            Skills = serverData.skills.Select(SkillToDto).ToList(),
            Items = serverData.itemOrTags.Where(iot => !iot.IsTag).Select(s => ItemToDto(s, serverData.craftingTables, serverData.pluginModules)).ToList(),
            Tags = serverData.itemOrTags.Where(iot => iot.IsTag).Select(TagToDto).ToList(),
            Recipes = serverData.recipes.Select(RecipeToDto).ToList(),
        };
    }

    private void ImportSkills(Server server, List<SkillDto> newSkills)
    {
        var nameOccurence = new Dictionary<string, int>();

        foreach (var newSkill in newSkills)
        {
            nameOccurence.Add(newSkill.Name, 1);

            var dbSkill = serverDataService.Skills.FirstOrDefault(s => s.Name == newSkill.Name);

            if (dbSkill is null)
            {
                dbSkill = serverDataService.ImportSkill(server, newSkill.Name, TranslationsToLocalizedField(server, newSkill.LocalizedName), newSkill.Profession, newSkill.MaxLevel, newSkill.LaborReducePercent);
            }
            else
            {
                serverDataService.RefreshSkill(dbSkill, TranslationsToLocalizedField(server, newSkill.LocalizedName, dbSkill.LocalizedName), newSkill.Profession, newSkill.MaxLevel, newSkill.LaborReducePercent);
            }

            ImportTalents(dbSkill, newSkill.Talents);
        }

        foreach (var dbSkill in serverDataService.Skills.Where(dbSkill => !nameOccurence.TryGetValue(dbSkill.Name, out _)))
        {
            serverDataService.DeleteSkill(dbSkill);
        }
    }

    private void ImportTalents(Skill skill, List<TalentDto> newTalents)
    {
        var nameOccurence = new Dictionary<string, int>();

        foreach (var newTalent in newTalents)
        {
            nameOccurence.Add(newTalent.Name, 1);

            var dbTalent = skill.Talents.FirstOrDefault(s => s.Name == newTalent.Name);

            if (dbTalent is null)
            {
                serverDataService.ImportTalent(skill, newTalent.Name, TranslationsToLocalizedField(skill.Server, newTalent.LocalizedName), newTalent.TalentGroupName, newTalent.Level, newTalent.Value);
            }
            else
            {
                serverDataService.RefreshTalent(dbTalent, skill, TranslationsToLocalizedField(skill.Server, newTalent.LocalizedName, dbTalent.LocalizedName), newTalent.TalentGroupName, newTalent.Level, newTalent.Value);
            }
        }

        foreach (var dbTalent in skill.Talents.Where(dbTalent => !nameOccurence.TryGetValue(dbTalent.Name, out _)))
        {
            serverDataService.DeleteTalent(dbTalent);
        }
    }

    private int ImportItems(Server server, List<ItemDto> items, out string[] itemErrorNames)
    {
        var errorCount = 0;
        var errorNames = new List<string>();

        {
            var nameOccurence = new Dictionary<string, int>();

            foreach (var item in items.Where(i => i.IsPluginModule is true))
            {
                nameOccurence.Add(item.Name, 1);

                ImportPluginModule(server, item);
            }

            foreach (var dbPluginModule in serverDataService.PluginModules.Where(dbPluginModule => !nameOccurence.TryGetValue(dbPluginModule.Name, out _)))
            {
                serverDataService.DeletePluginModule(dbPluginModule);
            }
        }

        {
            var nameOccurence = new Dictionary<string, int>();

            foreach (var item in items.Where(i => i.IsCraftingTable is true))
            {
                nameOccurence.Add(item.Name, 1);

                var error = ImportCraftingTable(server, item);

                if (error <= 0) continue;

                errorCount += error;
                errorNames.Add(item.Name);
            }

            foreach (var dbCraftingTable in serverDataService.CraftingTables.Where(dbCraftingTable => !nameOccurence.TryGetValue(dbCraftingTable.Name, out _)))
            {
                serverDataService.DeleteCraftingTable(dbCraftingTable);
            }
        }

        {
            var nameOccurence = new Dictionary<string, int>();

            foreach (var item in items)
            {
                nameOccurence.Add(item.Name, 1);

                ImportItem(server, item);
            }

            foreach (var dbItem in serverDataService.ItemOrTags.Where(iot => !iot.IsTag))
            {
                if (!nameOccurence.TryGetValue(dbItem.Name, out _))
                {
                    serverDataService.DeleteItemOrTag(dbItem);
                }
            }
        }

        itemErrorNames = errorNames.ToArray();
        return errorCount;
    }

    private void ImportPluginModule(Server server, ItemDto pluginModule)
    {
        var dbPluginModule =
            serverDataService.PluginModules.FirstOrDefault(p => p.Name == pluginModule.Name);
        var dbSkill = serverDataService.Skills.FirstOrDefault(s => s.Name == pluginModule.PluginModuleSkill);
        var pluginType = pluginModule.PluginType switch
        {
            "Resource" => PluginType.Resource,
            "Speed" => PluginType.Speed,
            "Resource&Speed" => PluginType.ResourceAndSpeed,
            _ => PluginType.None
        } ;

        if (dbPluginModule is null)
        {
            serverDataService.ImportPluginModule(server, pluginModule.Name, TranslationsToLocalizedField(server, pluginModule.LocalizedName), pluginType, (decimal)pluginModule.PluginModulePercent!, dbSkill, pluginModule.PluginModuleSkillPercent);
        }
        else
        {
            serverDataService.RefreshPluginModule(dbPluginModule, TranslationsToLocalizedField(server, pluginModule.LocalizedName, dbPluginModule.LocalizedName), pluginType, (decimal)pluginModule.PluginModulePercent!, dbSkill, pluginModule.PluginModuleSkillPercent);
        }
    }

    private int ImportCraftingTable(Server server, ItemDto craftingTable)
    {
        try
        {
            var dbCraftingTable = serverDataService.CraftingTables.FirstOrDefault(p => p.Name == craftingTable.Name);

            var pluginModules = craftingTable.CraftingTablePluginModules?
                .Select(ctpm => serverDataService.PluginModules.First(pm => pm.Name == ctpm))
                .ToList() ?? [];

            if (dbCraftingTable is null)
            {
                serverDataService.ImportCraftingTable(
                    server,
                    craftingTable.Name,
                    TranslationsToLocalizedField(server, craftingTable.LocalizedName),
                    pluginModules
                );
            }
            else
            {
                serverDataService.RefreshCraftingTable(
                    dbCraftingTable,
                    TranslationsToLocalizedField(server, craftingTable.LocalizedName, dbCraftingTable.LocalizedName),
                    pluginModules
                );
            }
        }
        catch (Exception)
        {
            return 1;
        }

        return 0;
    }

    private ItemOrTag ImportItem(Server server, EcoItemDto item, bool isTag = false)
    {
        var dbItem = serverDataService.ItemOrTags.FirstOrDefault(p => p.Name == item.Name);

        if (dbItem is null)
        {
            dbItem = serverDataService.ImportItemOrTag(server, item.Name, TranslationsToLocalizedField(server, item.LocalizedName), isTag);
        }
        else
        {
            serverDataService.RefreshItemOrTag(dbItem, TranslationsToLocalizedField(server, item.LocalizedName, dbItem.LocalizedName), isTag);
        }

        return dbItem;
    }

    private void ImportTags(Server server, List<TagDto> tags)
    {
        var nameOccurence = new Dictionary<string, int>();

        foreach (var tag in tags)
        {
            nameOccurence.Add(tag.Name, 1);

            var dbTag = ImportItem(server, tag, true);

            dbTag.AssociatedItems = tag.AssociatedItems
                .Select(i => serverDataService.ItemOrTags.FirstOrDefault(iot => iot.Name == i))
                .Where(e => e is not null)
                .ToList()!;
        }

        foreach (var dbTag in serverDataService.ItemOrTags.Where(iot => iot.IsTag))
        {
            if (!nameOccurence.TryGetValue(dbTag.Name, out _))
            {
                serverDataService.DeleteItemOrTag(dbTag);
            }
        }
    }

    private int ImportRecipes(Server server, List<RecipeDto> recipes, out string[] recipeErrorNames)
    {
        var errorCount = 0;
        var errorNames = new List<string>();
        var nameOccurence = new Dictionary<string, int>();

        foreach (var recipe in recipes)
        {
            try
            {
                if (nameOccurence.TryGetValue(recipe.Name, out var index))
                {
                    nameOccurence[recipe.Name] += 1;
                    recipe.Name += $"__{index}";
                }
                else
                {
                    nameOccurence[recipe.Name] = 1;
                }

                var dbRecipe = serverDataService.Recipes.FirstOrDefault(p => p.Name == recipe.Name);
                var dbSkill = serverDataService.Skills.FirstOrDefault(s => s.Name == recipe.RequiredSkill);
                CraftingTable dbCraftingTable;
                try
                {
                    dbCraftingTable = serverDataService.CraftingTables.First(c => c.Name == recipe.CraftingTable);
                }
                catch (Exception e)
                {
                    throw new Exception("Missing CraftingTable: " + recipe.CraftingTable, e);
                }

                if (dbRecipe is null)
                {
                    dbRecipe = serverDataService.ImportRecipe(
                        server,
                        recipe.Name,
                        TranslationsToLocalizedField(server, recipe.LocalizedName),
                        recipe.FamilyName,
                        dbSkill,
                        recipe.RequiredSkillLevel,
                        recipe.IsBlueprint,
                        recipe.IsDefault,
                        dbCraftingTable
                    );
                }
                else
                {
                    serverDataService.RefreshRecipe(
                        dbRecipe,
                        TranslationsToLocalizedField(server, recipe.LocalizedName, dbRecipe.LocalizedName),
                        recipe.FamilyName,
                        dbSkill,
                        recipe.RequiredSkillLevel,
                        recipe.IsBlueprint,
                        recipe.IsDefault,
                        dbCraftingTable
                    );
                }

                dbRecipe.Labor = ImportDynamicValue(server, dbRecipe.Labor, recipe.Labor);
                dbRecipe.CraftMinutes = ImportDynamicValue(server, dbRecipe.CraftMinutes, recipe.CraftMinutes);

                for (var i = 0; i < recipe.Ingredients.Count; i++)
                {
                    recipe.Ingredients[i].Quantity.BaseValue *= -1;
                    recipe.Ingredients[i].Index = i;
                }

                for (var i = 0; i < recipe.Products.Count; i++)
                {
                    recipe.Products[i].Index = i;
                }

                var dbElements = dbRecipe.Elements;
                dbRecipe.Elements = [];

                foreach (var element in recipe.Ingredients.Concat(recipe.Products))
                {
                    ItemOrTag dbItemOrTag;

                    try
                    {
                        dbItemOrTag = serverDataService.ItemOrTags.First(e => e.Name == element.ItemOrTag);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Missing ItemOrTag: " + element.ItemOrTag, e);
                    }

                    // element.Quantity * e.Quantity > 0 ensures "element" and "e" are both ingredients or products (You can have an itemOrTag both in ingredient and product => molds,
                    // so we need to be sure dbElement is the correct-retrieved element)
                    var dbElement = dbElements.FirstOrDefault(e =>
                        e.ItemOrTag.Name == element.ItemOrTag && element.Quantity.BaseValue * e.Quantity.BaseValue > 0);

                    // Specific for BarrelItem, still need to figure a way to auto calculate this
                    var specificBarrel = element is { ItemOrTag: "BarrelItem", Quantity.BaseValue: > 0, Index: > 0 };

                    if (dbElement is null)
                    {
                        dbElement = serverDataService.ImportElement(
                            dbRecipe,
                            dbItemOrTag,
                            element.Index,
                            specificBarrel || (element.Quantity.BaseValue > 0 && recipe.Ingredients.FirstOrDefault(e => e.ItemOrTag == element.ItemOrTag) is not null)
                        );
                    }
                    else
                    {
                        serverDataService.RefreshElement(
                            dbElement,
                            dbRecipe,
                            dbItemOrTag,
                            element.Index,
                            specificBarrel || (element.Quantity.BaseValue > 0 && recipe.Ingredients.FirstOrDefault(e => e.ItemOrTag == element.ItemOrTag) is not null)
                        );

                        dbRecipe.Elements.Add(dbElement);
                    }

                    dbElement.Quantity = ImportDynamicValue(server, dbElement.Quantity, element.Quantity);
                }

                var productsToEdit = dbRecipe.Elements.Where(e => e.IsProduct() && !e.DefaultIsReintegrated).OrderBy(e => e.Index).ToList();

                for (int i = 0; i < productsToEdit.Count; i++)
                {
                    productsToEdit[i].DefaultShare = (productsToEdit.Count > 1 ? i == 0 ? 0.8m : 0.2m / (productsToEdit.Count - 1) : 1);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                errorNames.Add(recipe.Name + $" ({ex.Message})");
            }
        }

        foreach (var dbRecipe in serverDataService.Recipes.Where(dbRecipe => !nameOccurence.TryGetValue(dbRecipe.Name, out _)))
        {
            serverDataService.DeleteRecipe(dbRecipe);
        }

        recipeErrorNames = errorNames.ToArray();
        return errorCount;
    }

    private DynamicValue ImportDynamicValue(Server server, DynamicValue? dbDynamicValue, DynamicValueDto newDynamicValue)
    {
        if (dbDynamicValue is null)
        {
            dbDynamicValue = serverDataService.ImportDynamicValue(newDynamicValue.BaseValue, server);
        }
        else
        {
            serverDataService.RefreshDynamicValue(dbDynamicValue, newDynamicValue.BaseValue);
        }

        ImportModifiers(dbDynamicValue, newDynamicValue.Modifiers);

        return dbDynamicValue;
    }

    private string GetModifierName(Modifier modifier) => $"{modifier.DynamicType}:{modifier.Skill?.Name ?? modifier.Talent?.Name}";
    private string GetModifierName(ModifierDto modifier) => $"{modifier.DynamicType}:{modifier.Item}";

    private void ImportModifiers(DynamicValue dynamicValue, List<ModifierDto> modifiers)
    {
        var nameOccurence = new Dictionary<string, int>();

        foreach (var modifier in modifiers)
        {
            nameOccurence.Add(GetModifierName(modifier), 1);

            var dbModifier = dynamicValue.Modifiers.FirstOrDefault(m => GetModifierName(m) == GetModifierName(modifier));
            ISLinkedToModifier? iSLinkedToModifier = null;

            switch (modifier.DynamicType)
            {
                case "Talent":
                    iSLinkedToModifier = serverDataService.Skills.SelectMany(s => s.Talents).FirstOrDefault(t => t.Name == modifier.Item);
                    break;
                case "Skill":
                case "Module":
                    iSLinkedToModifier = serverDataService.Skills.FirstOrDefault(t => t.Name == modifier.Item);
                    break;
            }

            if (iSLinkedToModifier is null)
                return;

            if (dbModifier is null)
            {
                serverDataService.ImportModifier(dynamicValue, modifier.DynamicType, iSLinkedToModifier);
            }
            else
            {
                serverDataService.RefreshModifier(dbModifier, modifier.DynamicType, iSLinkedToModifier);
            }
        }

        foreach (var dbModifier in dynamicValue.Modifiers.Where(dbModifier => !nameOccurence.TryGetValue(GetModifierName(dbModifier), out _)))
        {
            serverDataService.DeleteModifier(dbModifier);
        }
    }

    private static LocalizedField TranslationsToLocalizedField(Server server,
        Dictionary<LanguageCode, string> translations, LocalizedField? localizedField = null)
    {
        localizedField ??= new LocalizedField
        {
            Server = server
        };

        foreach (var translation in translations)
        {
            switch (translation.Key)
            {
                case LanguageCode.en_US:
                    localizedField.en_US = translation.Value;
                    break;
                case LanguageCode.fr:
                    localizedField.fr = translation.Value;
                    break;
                case LanguageCode.es:
                    localizedField.es = translation.Value;
                    break;
                case LanguageCode.de:
                    localizedField.de = translation.Value;
                    break;
                case LanguageCode.ko:
                    localizedField.ko = translation.Value;
                    break;
                case LanguageCode.pt_BR:
                    localizedField.pt_BR = translation.Value;
                    break;
                case LanguageCode.zh_Hans:
                    localizedField.zh_Hans = translation.Value;
                    break;
                case LanguageCode.ru:
                    localizedField.ru = translation.Value;
                    break;
                case LanguageCode.it:
                    localizedField.it = translation.Value;
                    break;
                case LanguageCode.pt_PT:
                    localizedField.pt_PT = translation.Value;
                    break;
                case LanguageCode.hu:
                    localizedField.hu = translation.Value;
                    break;
                case LanguageCode.ja:
                    localizedField.ja = translation.Value;
                    break;
                case LanguageCode.nn:
                    localizedField.nn = translation.Value;
                    break;
                case LanguageCode.pl:
                    localizedField.pl = translation.Value;
                    break;
                case LanguageCode.nl:
                    localizedField.nl = translation.Value;
                    break;
                case LanguageCode.ro:
                    localizedField.ro = translation.Value;
                    break;
                case LanguageCode.da:
                    localizedField.da = translation.Value;
                    break;
                case LanguageCode.cs:
                    localizedField.cs = translation.Value;
                    break;
                case LanguageCode.sv:
                    localizedField.sv = translation.Value;
                    break;
                case LanguageCode.uk:
                    localizedField.uk = translation.Value;
                    break;
                case LanguageCode.el:
                    localizedField.el = translation.Value;
                    break;
                case LanguageCode.ar_sa:
                    localizedField.ar_sa = translation.Value;
                    break;
                case LanguageCode.vi:
                    localizedField.vi = translation.Value;
                    break;
                case LanguageCode.tr:
                    localizedField.tr = translation.Value;
                    break;
                default:
                    throw new ArgumentException($"Unsupported LanguageCode: {translation.Key}");
            }
        }

        return localizedField;
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
            TalentGroupName = talent.TalentGroupName,
            Level = talent.Level,
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
        public required string TalentGroupName { get; init; }
        public required decimal Value { get; init; }
        public required int Level { get; init; }
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
    }

    private class ElementDto
    {
        public required string ItemOrTag { get; init; }
        public required DynamicValueDto Quantity { get; init; }

        // ! This is not in the json file, it's calculated after
        public int Index { get; set; }
    }
}

public class LanguageCodeDictionaryConverter : JsonConverter<Dictionary<LanguageCode, string>>
{
    public override Dictionary<LanguageCode, string> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<LanguageCode, string>();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("JSON is not an objet.");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Property expected.");
            }

            string propertyName = reader.GetString()!;

            string enumKey = propertyName.Replace("-", "_");

            if (!Enum.TryParse<LanguageCode>(enumKey, ignoreCase: true, out var languageCode))
            {
                throw new JsonException($"Invalid language code : {propertyName}");
            }

            reader.Read();

            string value = reader.GetString()!;

            dictionary.Add(languageCode, value);
        }

        throw new JsonException("End of expect JSON object.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<LanguageCode, string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            string key = kvp.Key.ToString().Replace("_", "-");

            writer.WritePropertyName(key);
            writer.WriteStringValue(kvp.Value);
        }

        writer.WriteEndObject();
    }
}
