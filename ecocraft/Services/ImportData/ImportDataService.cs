using System.Text.Json;
using System.Text.Json.Serialization;
using ecocraft.Models;

namespace ecocraft.Services.ImportData;

public class ImportDataService(
    EcoCraftDbContext dbContext,
    ServerDataService serverDataService)
{
    public async Task ImportServerData(string jsonContent, Server server)
    {
        try
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new LanguageCodeDictionaryConverter());

            var importedData = JsonSerializer.Deserialize<ImportDataDto>(jsonContent, options);

            if (importedData is not null)
            {
                ImportSkills(server, importedData.skills);
                ImportPluginModules(server, importedData.pluginModules);
                ImportCraftingTables(server, importedData.craftingTables);
                ImportRecipes(server, importedData.recipes);
                ImportItemTagAssoc(importedData.itemTagAssoc);

                await dbContext.SaveChangesAsync();
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Erreur lors de la désérialisation JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur durant l'import: {ex.Message} {ex.StackTrace}");
        }
    }

    private void ImportSkills(Server server, List<SkillDto> newSkills)
    {
        foreach (var newSkill in newSkills)
        {
            var dbSkill = serverDataService.Skills.FirstOrDefault(s => s.Name == newSkill.Name);

            if (dbSkill is null)
            {
                serverDataService.ImportSkill(server, newSkill.Name,
                    TranslationsToLocalizedField(server, newSkill.LocalizedName), newSkill.Profession);
            }
            else
            {
                serverDataService.RefreshSkill(dbSkill, TranslationsToLocalizedField(server, newSkill.LocalizedName, dbSkill.LocalizedName),
                    newSkill.Profession);
            }
        }
    }

    private void ImportPluginModules(Server server, List<PluginModuleDto> newPluginModules)
    {
        foreach (var newPluginModule in newPluginModules)
        {
            var dbPluginModule =
                serverDataService.PluginModules.FirstOrDefault(p => p.Name == newPluginModule.Name);

            if (dbPluginModule is null)
            {
                serverDataService.ImportPluginModule(server, newPluginModule.Name,
                    TranslationsToLocalizedField(server, newPluginModule.LocalizedName), newPluginModule.Percent);
            }
            else
            {
                serverDataService.RefreshPluginModule(dbPluginModule,
                    TranslationsToLocalizedField(server, newPluginModule.LocalizedName, dbPluginModule.LocalizedName), newPluginModule.Percent);
            }
        }
    }

    private void ImportCraftingTables(Server server, List<CraftingTableDto> newCraftingTables)
    {
        foreach (var newCraftingTable in newCraftingTables)
        {
            var dbCraftingTable =
                serverDataService.CraftingTables.FirstOrDefault(p => p.Name == newCraftingTable.Name);

            var pluginModules = newCraftingTable.CraftingTablePluginModules
                .Select(ctpm => serverDataService.PluginModules.First(pm => pm.Name == ctpm))
                .ToList();

            if (dbCraftingTable is null)
            {
                serverDataService.ImportCraftingTable(
                    server,
                    newCraftingTable.Name,
                    TranslationsToLocalizedField(server, newCraftingTable.LocalizedName),
                    pluginModules
                );
            }
            else
            {
                serverDataService.RefreshCraftingTable(
                    dbCraftingTable,
                    TranslationsToLocalizedField(server, newCraftingTable.LocalizedName, dbCraftingTable.LocalizedName),
                    pluginModules
                );
            }
        }
    }

    private void ImportRecipes(Server server, List<RecipeDto> newRecipes)
    {
        foreach (var newRecipe in newRecipes)
        {
            var dbRecipe = serverDataService.Recipes.FirstOrDefault(p => p.Name == newRecipe.Name);
            var dbSkill = serverDataService.Skills.FirstOrDefault(s => s.Name == newRecipe.RequiredSkill);
            var dbCraftingTable = serverDataService.CraftingTables.First(c => c.Name == newRecipe.CraftingTable);

            if (dbRecipe is null)
            {
                dbRecipe = serverDataService.ImportRecipe(
                    server,
                    newRecipe.Name,
                    TranslationsToLocalizedField(server, newRecipe.LocalizedName),
                    newRecipe.FamilyName,
                    newRecipe.CraftMinutes,
                    dbSkill,
                    newRecipe.RequiredSkillLevel,
                    newRecipe.IsBlueprint,
                    newRecipe.IsDefault,
                    newRecipe.Labor,
                    dbCraftingTable
                );
            }
            else
            {
                serverDataService.RefreshRecipe(
                    dbRecipe,
                    TranslationsToLocalizedField(server, newRecipe.LocalizedName, dbRecipe.LocalizedName),
                    newRecipe.FamilyName,
                    newRecipe.CraftMinutes,
                    dbSkill,
                    newRecipe.RequiredSkillLevel,
                    newRecipe.IsBlueprint,
                    newRecipe.IsDefault,
                    newRecipe.Labor,
                    dbCraftingTable
                );
            }

            // Should we do that ??
            /*foreach (var product in newRecipe.Products.ToList())
            {
                var associatedIngredient = newRecipe.Ingredients.FirstOrDefault(i => i.ItemOrTag == product.ItemOrTag);
                
                if (associatedIngredient is not null)
                {
                    newRecipe.Products.Remove(product);
                    associatedIngredient.Quantity += product.Quantity;
                }
            }*/

            for (var i = 0; i < newRecipe.Ingredients.Count; i++)
            {
                newRecipe.Ingredients[i].Quantity *= -1;
                newRecipe.Ingredients[i].Index = i;
            }

            for (var i = 0; i < newRecipe.Products.Count; i++)
            {
                newRecipe.Products[i].Index = i;
            }

            var dbElements = dbRecipe.Elements;
            dbRecipe.Elements = [];

            foreach (var element in newRecipe.Ingredients.Concat(newRecipe.Products))
            {
                var skill = serverDataService.Skills.FirstOrDefault(s => s.Name == element.Skill);
                var dbItemOrTag = serverDataService.ItemOrTags.FirstOrDefault(e => e.Name == element.ItemOrTag);

                if (dbItemOrTag is null)
                {
                    dbItemOrTag = serverDataService.ImportItemOrTag(
                        server,
                        element.ItemOrTag,
                        TranslationsToLocalizedField(server, element.LocalizedItemOrTag),
                        element.IsTag
                    );
                }
                else
                {
                    serverDataService.RefreshItemOrTag(
                        dbItemOrTag,
                        TranslationsToLocalizedField(server, element.LocalizedItemOrTag, dbItemOrTag.LocalizedName),
                        element.IsTag
                    );
                }

                // element.Quantity * e.Quantity > 0 ensures "element" and "e" are both ingredients or products (You can have an itemOrTag both in ingredient and product => molds,
                // so we need to be sure dbElement is the correct-retrieved element) 
                var dbElement = dbElements.FirstOrDefault(e =>
                    e.ItemOrTag.Name == element.ItemOrTag && element.Quantity * e.Quantity > 0);

                if (dbElement is null)
                {
                    dbElement = serverDataService.ImportElement(
                        dbRecipe,
                        dbItemOrTag,
                        element.Index,
                        element.Quantity,
                        element.IsDynamic,
                        skill,
                        element.LavishTalent
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
                        element.LavishTalent
                    );
                }

                dbRecipe.Elements.Add(dbElement);
            }
        }
    }

    private void ImportItemTagAssoc(List<ItemTagAssocDto> newItemOrTags)
    {
        foreach (var newItemOrTag in newItemOrTags)
        {
            var dbTag = serverDataService.ItemOrTags.FirstOrDefault(i => i.Name == newItemOrTag.Tag);

            // We import itemTagAssociations only if the tag exists 
            if (dbTag is null) continue;

            dbTag.IsTag = true;
            dbTag.AssociatedItems.Clear();

            foreach (var item in newItemOrTag.Types)
            {
                var itemDb = serverDataService.ItemOrTags.FirstOrDefault(i => i.Name == item);

                // We import itemTagAssociations only if the item exist
                if (itemDb is null) continue;

                dbTag.AssociatedItems.Add(itemDb);
            }
        }
    }

    public static LocalizedField TranslationsToLocalizedField(Server server, Dictionary<LanguageCode, string> translations, LocalizedField? localizedField = null)
    {
        if (localizedField is null)
        {
            localizedField = new LocalizedField
            {
                Server = server
            };
        }

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

    private class ImportDataDto
    {
        public List<RecipeDto> recipes { get; set; }
        public List<ItemTagAssocDto> itemTagAssoc { get; set; }
        public List<PluginModuleDto> pluginModules { get; set; }
        public List<CraftingTableDto> craftingTables { get; set; }
        public List<SkillDto> skills { get; set; }
    }

    private class RecipeDto
    {
        public string Name { get; set; }
        public Dictionary<LanguageCode, string> LocalizedName { get; set; }
        public string FamilyName { get; set; }
        public float CraftMinutes { get; set; }
        public string RequiredSkill { get; set; }
        public int RequiredSkillLevel { get; set; }
        public bool IsBlueprint { get; set; }
        public bool IsDefault { get; set; }
        public float Labor { get; set; }
        public string CraftingTable { get; set; }
        public List<ElementDto> Ingredients { get; set; }
        public List<ElementDto> Products { get; set; }
    }

    private class ElementDto
    {
        public string ItemOrTag { get; set; }
        public Dictionary<LanguageCode, string> LocalizedItemOrTag { get; set; }
        public bool IsTag { get; set; }
        public int Index { get; set; } // ! This is not in the json file, it's calculated after
        public float Quantity { get; set; }
        public bool IsDynamic { get; set; }
        public string Skill { get; set; }
        public bool LavishTalent { get; set; }
    }

    private class CraftingTableDto
    {
        public string Name { get; set; }
        public Dictionary<LanguageCode, string> LocalizedName { get; set; }
        public List<string> CraftingTablePluginModules { get; set; }
    }

    private class ItemTagAssocDto
    {
        public string Tag { get; set; }
        public List<string> Types { get; set; }
    }

    private class PluginModuleDto
    {
        public string Name { get; set; }
        public Dictionary<LanguageCode, string> LocalizedName { get; set; }
        public float Percent { get; set; }
    }

    private class SkillDto
    {
        public string Name { get; set; }
        public Dictionary<LanguageCode, string> LocalizedName { get; set; }
        public string? Profession { get; set; }
    }
}

public class LanguageCodeDictionaryConverter : JsonConverter<Dictionary<LanguageCode, string>>
{
    public override Dictionary<LanguageCode, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<LanguageCode, string>();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Le JSON n'est pas un objet.");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Propriété attendue.");
            }

            string propertyName = reader.GetString();

            // Remplacer les tirets par des underscores
            string enumKey = propertyName.Replace("-", "_");

            if (!Enum.TryParse<LanguageCode>(enumKey, ignoreCase: true, out var languageCode))
            {
                throw new JsonException($"Clé de langue invalide : {propertyName}");
            }

            reader.Read();

            string value = reader.GetString();

            dictionary.Add(languageCode, value);
        }

        throw new JsonException("Fin de l'objet JSON attendue.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<LanguageCode, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            // Convertir la clé en chaîne avec des tirets
            string key = kvp.Key.ToString().Replace("_", "-");

            writer.WritePropertyName(key);
            writer.WriteStringValue(kvp.Value);
        }

        writer.WriteEndObject();
    }
}