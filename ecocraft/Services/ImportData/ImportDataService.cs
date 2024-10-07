using System.Text.Json;
using ecocraft.Models;

namespace ecocraft.Services.ImportData
{
    public class ImportDataService(
        EcoCraftDbContext dbContext,
        ServerDataService serverDataService)
    {
        public async Task ImportServerData(string jsonContent, Server server)
        {
            try
            {
                var importedData = JsonSerializer.Deserialize<ImportDataDto>(jsonContent);

                if (importedData is not null)
                {
                    ImportSkills(server, importedData.skills, importedData.itemTagAssoc);
                    ImportPluginModules(server, importedData.pluginModules);
                    ImportCraftingTables(server, importedData.craftingTables);
                    ImportItemTag(server, importedData.itemTagAssoc);
                    ImportRecipes(server, importedData.recipes);
                    
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

        private void ImportSkills(Server server, List<SkillDto> newSkills, List<ItemTagAssocDto> newItemTagAssocs)
        {
            foreach (var newSkill in newSkills)
            {
                var dbSkill = serverDataService.Skills.FirstOrDefault(s => s.Name == newSkill.Name);

                if (dbSkill is null)
                {
                    serverDataService.ImportSkill(server, newSkill.Name, newSkill.Profession);
                }
                else
                {
                    serverDataService.RefreshSkill(dbSkill, newSkill.Profession);
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
                    serverDataService.ImportPluginModule(server, newPluginModule.Name, newPluginModule.Percent);
                }
                else
                {
                    serverDataService.RefreshPluginModule(dbPluginModule, newPluginModule.Percent);
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
                        pluginModules
                    );
                }
                else
                {
                    serverDataService.RefreshCraftingTable(
                        dbCraftingTable,
                        pluginModules
                    );
                }
            }
        }

        private void ImportItemTag(Server server, List<ItemTagAssocDto> newItemOrTags)
        {
            foreach (var newItemOrTag in newItemOrTags)
            {
                var dbTag = serverDataService.ItemOrTags.FirstOrDefault(i => i.Name == newItemOrTag.Tag) ?? serverDataService.ImportItemOrTag(server, newItemOrTag.Tag, true);
                dbTag.AssociatedItemOrTags.Clear();

                foreach (var item in newItemOrTag.Types)
                {
                    var itemDb = serverDataService.ItemOrTags.FirstOrDefault(i => i.Name == item) ?? serverDataService.ImportItemOrTag(server, item, false);
                    dbTag.AssociatedItemOrTags.Add(itemDb);
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

                foreach (var newRecipeIngredient in newRecipe.Ingredients)
                {
                    newRecipeIngredient.Quantity *= -1;
                }

                var dbElements = dbRecipe.Elements;
                dbRecipe.Elements = [];

                foreach (var element in newRecipe.Ingredients.Concat(newRecipe.Products))
                {
                    var skill = serverDataService.Skills.FirstOrDefault(s => s.Name == element.Skill);
                    var itemOrTag = serverDataService.ItemOrTags.FirstOrDefault(e => e.Name == element.ItemOrTag);

                    if (itemOrTag is null)
                    {
                        // This can't be a tag otherwise it would already exist
                        itemOrTag = serverDataService.ImportItemOrTag(server, element.ItemOrTag, false);
                    }

                    // element.Quantity * e.Quantity > 0 ensures "element" and "e" are both ingredients or products (You can have an itemOrTag both in ingredient and product => molds,
                    // so we need to be sure dbElement is the correct-retrieved element) 
                    var dbElement = dbElements.FirstOrDefault(e =>
                        e.ItemOrTag.Name == element.ItemOrTag && element.Quantity * e.Quantity > 0);

                    if (dbElement is null)
                    {
                        dbElement = serverDataService.ImportElement(dbRecipe, itemOrTag, element.Quantity,
                            element.IsDynamic, skill, element.LavishTalent);
                    }
                    else
                    {
                        serverDataService.RefreshElement(dbElement, dbRecipe, itemOrTag, element.Quantity,
                            element.IsDynamic, skill, element.LavishTalent);
                    }

                    dbRecipe.Elements.Add(dbElement);
                }
            }
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
            public float Quantity { get; set; }
            public bool IsDynamic { get; set; }
            public string Skill { get; set; }
            public bool LavishTalent { get; set; }
        }

        private class CraftingTableDto
        {
            public string Name { get; set; }
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
            public float Percent { get; set; }
        }

        private class SkillDto
        {
            public string Name { get; set; }
            public string? Profession { get; set; }
        }
    }
}