using System.Text.Json;
using System.Text.Json.Serialization;
using ecocraft.Extensions;
using ecocraft.Models;

namespace ecocraft.Services.ImportData;

public class ImportException : Exception;

public class ImportDataService(
    EcoCraftDbContext dbContext,
    ServerDataService serverDataService)
{
    public async Task<(int, string[])> ImportServerData(string jsonContent, Server server)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LanguageCodeDictionaryConverter());

        var importedData = JsonSerializer.Deserialize<ImportDataDto>(jsonContent, options);
        var errorCount = 0;

        if (importedData is null) throw new ImportException();

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
                serverDataService.ImportSkill(server, newSkill.Name,
                    TranslationsToLocalizedField(server, newSkill.LocalizedName), newSkill.Profession,
                    newSkill.LaborReducePercent, newSkill.LavishTalentValue);
            }
            else
            {
                serverDataService.RefreshSkill(dbSkill,
                    TranslationsToLocalizedField(server, newSkill.LocalizedName, dbSkill.LocalizedName),
                    newSkill.Profession, newSkill.LaborReducePercent, newSkill.LavishTalentValue);
            }
        }

        foreach (var dbSkill in serverDataService.Skills.Where(dbSkill => !nameOccurence.TryGetValue(dbSkill.Name, out _)))
        {
            serverDataService.DeleteSkill(dbSkill);
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

        if (dbPluginModule is null)
        {
            serverDataService.ImportPluginModule(server, pluginModule.Name, TranslationsToLocalizedField(server, pluginModule.LocalizedName), (decimal)pluginModule.PluginModulePercent!);
        }
        else
        {
            serverDataService.RefreshPluginModule(dbPluginModule, TranslationsToLocalizedField(server, pluginModule.LocalizedName, dbPluginModule.LocalizedName), (decimal)pluginModule.PluginModulePercent!);
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
                    recipe.Name += $"__{index}";
                    nameOccurence[recipe.Name] += 1;
                }
                else
                {
                    nameOccurence[recipe.Name] = 1;
                }

                var dbRecipe = serverDataService.Recipes.FirstOrDefault(p => p.Name == recipe.Name);
                var dbSkill = serverDataService.Skills.FirstOrDefault(s => s.Name == recipe.RequiredSkill);
                var dbCraftingTable = serverDataService.CraftingTables.First(c => c.Name == recipe.CraftingTable);

                if (dbRecipe is null)
                {
                    dbRecipe = serverDataService.ImportRecipe(
                        server,
                        recipe.Name,
                        TranslationsToLocalizedField(server, recipe.LocalizedName),
                        recipe.FamilyName,
                        recipe.CraftMinutes,
                        dbSkill,
                        recipe.RequiredSkillLevel,
                        recipe.IsBlueprint,
                        recipe.IsDefault,
                        recipe.Labor,
                        dbCraftingTable
                    );
                }
                else
                {
                    serverDataService.RefreshRecipe(
                        dbRecipe,
                        TranslationsToLocalizedField(server, recipe.LocalizedName, dbRecipe.LocalizedName),
                        recipe.FamilyName,
                        recipe.CraftMinutes,
                        dbSkill,
                        recipe.RequiredSkillLevel,
                        recipe.IsBlueprint,
                        recipe.IsDefault,
                        recipe.Labor,
                        dbCraftingTable
                    );
                }

                for (var i = 0; i < recipe.Ingredients.Count; i++)
                {
                    recipe.Ingredients[i].Quantity *= -1;
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
                    var skill = serverDataService.Skills.FirstOrDefault(s => s.Name == element.Skill);
                    var dbItemOrTag = serverDataService.ItemOrTags.First(e => e.Name == element.ItemOrTag);

                    // element.Quantity * e.Quantity > 0 ensures "element" and "e" are both ingredients or products (You can have an itemOrTag both in ingredient and product => molds,
                    // so we need to be sure dbElement is the correct-retrieved element)
                    var dbElement = dbElements.FirstOrDefault(e =>
                        e.ItemOrTag.Name == element.ItemOrTag && element.Quantity * e.Quantity > 0);

                    // Specific for BarrelItem, still need to figure a way to auto calculate this
                    var specificBarrel = element is { ItemOrTag: "BarrelItem", Quantity: > 0, Index: > 0 };

                    if (dbElement is null)
                    {
                        serverDataService.ImportElement(
                            dbRecipe,
                            dbItemOrTag,
                            element.Index,
                            element.Quantity,
                            element.IsDynamic,
                            skill,
                            element.LavishTalent,
                            element.Quantity < 0 ? recipe.Ingredients.Count - 1 : recipe.Products.Count - 1,
                            specificBarrel || (element.Quantity > 0 && recipe.Ingredients.FirstOrDefault(e => e.ItemOrTag == element.ItemOrTag) is not null)
                        );
                    }
                    else
                    {
                        serverDataService.RefreshElement(
                            dbElement,
                            dbRecipe,
                            dbItemOrTag,
                            element.Index,
                            element.Quantity,
                            element.IsDynamic,
                            skill,
                            element.LavishTalent,
                            element.Quantity < 0 ? recipe.Ingredients.Count - 1 : recipe.Products.Count - 1,
                            specificBarrel || (element.Quantity > 0 && recipe.Ingredients.FirstOrDefault(e => e.ItemOrTag == element.ItemOrTag) is not null)
                        );

                        dbRecipe.Elements.Add(dbElement);
                    }
                }
            }
            catch (Exception)
            {
                errorCount++;
                errorNames.Add(recipe.Name);
            }
        }

        foreach (var dbRecipe in serverDataService.Recipes.Where(dbRecipe => !nameOccurence.TryGetValue(dbRecipe.Name, out _)))
        {
            serverDataService.DeleteRecipe(dbRecipe);
        }

        recipeErrorNames = errorNames.ToArray();
        return errorCount;
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

    private class ImportDataDto()
    {
        public List<SkillDto> Skills { get; init; } = [];
        public List<ItemDto> Items { get; init; } = [];
        public List<TagDto> Tags { get; init; } = [];
        public List<RecipeDto> Recipes { get; init; } = [];
    }

    private class EcoItemDto
    {
        public string Name { get; set; }
        public Dictionary<LanguageCode, string> LocalizedName { get; set; }
    }

    private class SkillDto : EcoItemDto
    {
        public string? Profession { get; set; }
        public decimal[] LaborReducePercent { get; set; }
        public decimal? LavishTalentValue { get; set; }
    }

    private class ItemDto : EcoItemDto
    {
        public bool? IsPluginModule { get; set; }
        public decimal? PluginModulePercent { get; set; }
        public bool? IsCraftingTable { get; set; }
        public List<string>? CraftingTablePluginModules { get; set; }
    }

    private class TagDto : EcoItemDto
    {
        public List<string> AssociatedItems { get; set; }
    }

    private class RecipeDto : EcoItemDto
    {
        public string FamilyName { get; set; }
        public decimal CraftMinutes { get; set; }
        public string RequiredSkill { get; set; }
        public int RequiredSkillLevel { get; set; }
        public bool IsBlueprint { get; set; }
        public bool IsDefault { get; set; }
        public decimal Labor { get; set; }
        public string CraftingTable { get; set; }
        public List<ElementDto> Ingredients { get; set; }
        public List<ElementDto> Products { get; set; }
    }

    private class ElementDto
    {
        public string ItemOrTag { get; set; }
        public decimal Quantity { get; set; }
        public bool IsDynamic { get; set; }
        public string Skill { get; set; }
        public bool LavishTalent { get; set; }

        // ! This is not in the json file, it's calculated after
        public int Index { get; set; }
    }

    private static Dictionary<LanguageCode, string> LocalizedFieldToDto(LocalizedField localizedField)
    {
        var result = new Dictionary<LanguageCode, string>();

        result.Add(LanguageCode.en_US, localizedField.en_US);
        result.Add(LanguageCode.fr, localizedField.fr);
        result.Add(LanguageCode.es, localizedField.es);
        result.Add(LanguageCode.de, localizedField.de);
        result.Add(LanguageCode.ko, localizedField.ko);
        result.Add(LanguageCode.pt_BR, localizedField.pt_BR);
        result.Add(LanguageCode.zh_Hans, localizedField.zh_Hans);
        result.Add(LanguageCode.ru, localizedField.ru);
        result.Add(LanguageCode.it, localizedField.it);
        result.Add(LanguageCode.pt_PT, localizedField.pt_PT);
        result.Add(LanguageCode.hu, localizedField.hu);
        result.Add(LanguageCode.ja, localizedField.ja);
        result.Add(LanguageCode.nn, localizedField.nn);
        result.Add(LanguageCode.pl, localizedField.pl);
        result.Add(LanguageCode.nl, localizedField.nl);
        result.Add(LanguageCode.ro, localizedField.ro);
        result.Add(LanguageCode.da, localizedField.da);
        result.Add(LanguageCode.cs, localizedField.cs);
        result.Add(LanguageCode.sv, localizedField.sv);
        result.Add(LanguageCode.uk, localizedField.uk);
        result.Add(LanguageCode.el, localizedField.el);
        result.Add(LanguageCode.ar_sa, localizedField.ar_sa);
        result.Add(LanguageCode.vi, localizedField.vi);
        result.Add(LanguageCode.tr, localizedField.tr);

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
            LavishTalentValue = skill.LavishTalentValue,
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
            itemDto.PluginModulePercent = associatedPluginModule.Percent;
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
        return new RecipeDto
        {
            Name = recipe.Name,
            LocalizedName = LocalizedFieldToDto(recipe.LocalizedName),
            Ingredients = recipe.Elements.Where(e => e.IsIngredient()).OrderBy(e => e.Index).Select(ElementToDto).ToList(),
            Products = recipe.Elements.Where(e => e.IsProduct()).OrderBy(e => e.Index).Select(ElementToDto).ToList(),
            Labor = recipe.Labor,
            CraftingTable = recipe.CraftingTable.Name,
            CraftMinutes = recipe.CraftMinutes,
            FamilyName = recipe.FamilyName,
            IsBlueprint = recipe.IsBlueprint,
            IsDefault = recipe.IsDefault,
            RequiredSkill = recipe.Skill?.Name ?? "",
            RequiredSkillLevel = (int)recipe.SkillLevel,
        };
    }

    private static ElementDto ElementToDto(Element element)
    {
        return new ElementDto
        {
            ItemOrTag = element.ItemOrTag.Name,
            Quantity = Math.Abs(element.Quantity),
            IsDynamic = element.IsDynamic,
            Skill = element.Skill?.Name ?? "",
            LavishTalent = element.LavishTalent,
        };
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

            string propertyName = reader.GetString();

            string enumKey = propertyName.Replace("-", "_");

            if (!Enum.TryParse<LanguageCode>(enumKey, ignoreCase: true, out var languageCode))
            {
                throw new JsonException($"Invalid language code : {propertyName}");
            }

            reader.Read();

            string value = reader.GetString();

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
