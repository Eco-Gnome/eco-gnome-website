using ecocraft.Models;

namespace ecocraft.Services.ImportData;

public partial class ImportDataService
{
    private void ImportSkills(EcoCraftDbContext context, Server server, List<SkillDto> newSkills)
    {
        var nameOccurence = new Dictionary<string, int>();

        foreach (var newSkill in newSkills)
        {
            nameOccurence.Add(newSkill.Name, 1);

            var dbSkill = Skills.FirstOrDefault(s => s.Name == newSkill.Name);

            if (dbSkill is null)
            {
                dbSkill = ImportSkill(context, server, newSkill.Name, TranslationsToLocalizedField(context, server, newSkill.LocalizedName), newSkill.Profession, newSkill.MaxLevel, newSkill.LaborReducePercent);
            }
            else
            {
                RefreshSkill(context, dbSkill, TranslationsToLocalizedField(context, server, newSkill.LocalizedName, dbSkill.LocalizedName), newSkill.Profession, newSkill.MaxLevel, newSkill.LaborReducePercent);
            }

            ImportTalents(context, dbSkill, newSkill.Talents);
        }

        foreach (var dbSkill in Skills.Where(dbSkill => !nameOccurence.TryGetValue(dbSkill.Name, out _)).ToList())
        {
            DeleteSkill(context, dbSkill);
        }
    }

    private void ImportTalents(EcoCraftDbContext context, Skill skill, List<TalentDto> newTalents)
    {
        var nameOccurence = new Dictionary<string, int>();

        foreach (var newTalent in newTalents)
        {
            nameOccurence.Add(newTalent.Name, 1);

            var dbTalent = skill.Talents.FirstOrDefault(s => s.Name == newTalent.Name);

            if (dbTalent is null)
            {
                ImportTalent(
                    context,
                    skill,
                    newTalent.Name,
                    TranslationsToLocalizedField(context, skill.Server, newTalent.LocalizedName),
                    TranslationsToLocalizedField(context, skill.Server, newTalent.LocalizedDescription),
                    newTalent.TalentGroupName,
                    newTalent.Level,
                    newTalent.MaxLevel,
                    newTalent.Bonuses
                );
            }
            else
            {
                RefreshTalent(
                    context,
                    dbTalent,
                    skill,
                    TranslationsToLocalizedField(context, skill.Server, newTalent.LocalizedName, dbTalent.LocalizedName),
                    TranslationsToLocalizedField(context, skill.Server, newTalent.LocalizedDescription, dbTalent.LocalizedDescription),
                    newTalent.TalentGroupName,
                    newTalent.Level,
                    newTalent.MaxLevel,
                    newTalent.Bonuses
                );
            }
        }

        foreach (var dbTalent in skill.Talents.Where(dbTalent => !nameOccurence.TryGetValue(dbTalent.Name, out _)).ToList())
        {
            DeleteTalent(context, dbTalent);
        }
    }

    private int ImportItems(EcoCraftDbContext context, Server server, List<ItemDto> items, out string[] itemErrorNames)
    {
        var errorCount = 0;
        var errorNames = new List<string>();

        {
            var nameOccurence = new Dictionary<string, int>();

            foreach (var item in items.Where(i => i.IsPluginModule is true))
            {
                nameOccurence.Add(item.Name, 1);

                ImportPluginModule(context, server, item);
            }

            foreach (var dbPluginModule in PluginModules.Where(dbPluginModule => !nameOccurence.TryGetValue(dbPluginModule.Name, out _)).ToList())
            {
                DeletePluginModule(context, dbPluginModule);
            }
        }

        {
            var nameOccurence = new Dictionary<string, int>();

            foreach (var item in items.Where(i => i.IsCraftingTable is true))
            {
                nameOccurence.Add(item.Name, 1);

                var error = ImportCraftingTable(context, server, item);

                if (error <= 0) continue;

                errorCount += error;
                errorNames.Add(item.Name);
            }

            foreach (var dbCraftingTable in CraftingTables.Where(dbCraftingTable => !nameOccurence.TryGetValue(dbCraftingTable.Name, out _)).ToList())
            {
                DeleteCraftingTable(context, dbCraftingTable);
            }
        }

        {
            var nameOccurence = new Dictionary<string, int>();

            foreach (var item in items)
            {
                nameOccurence.Add(item.Name, 1);

                var dbItem = ImportItem(context, server, item);
                ApplyExportedItemFields(dbItem, item);
            }

            foreach (var dbItem in ItemOrTags.Where(iot => !iot.IsTag).ToList())
            {
                if (!nameOccurence.TryGetValue(dbItem.Name, out _))
                {
                    DeleteItemOrTag(context, dbItem);
                }
            }
        }

        itemErrorNames = errorNames.ToArray();
        return errorCount;
    }

    private void ImportPluginModule(EcoCraftDbContext context, Server server, ItemDto pluginModule)
    {
        var dbPluginModule = PluginModules.FirstOrDefault(p => p.Name == pluginModule.Name);
        var dbSkill = Skills.FirstOrDefault(s => s.Name == pluginModule.PluginModuleSkill);
        var pluginType = pluginModule.PluginType switch
        {
            "Resource" => PluginType.Resource,
            "Speed" => PluginType.Speed,
            "Resource&Speed" => PluginType.ResourceAndSpeed,
            _ => PluginType.None
        } ;

        if (dbPluginModule is null)
        {
            ImportPluginModule(context, server, pluginModule.Name, TranslationsToLocalizedField(context, server, pluginModule.LocalizedName), pluginType, (decimal)pluginModule.PluginModulePercent!, dbSkill, pluginModule.PluginModuleSkillPercent);
        }
        else
        {
            RefreshPluginModule(context, dbPluginModule, TranslationsToLocalizedField(context, server, pluginModule.LocalizedName, dbPluginModule.LocalizedName), pluginType, (decimal)pluginModule.PluginModulePercent!, dbSkill, pluginModule.PluginModuleSkillPercent);
        }
    }

    private int ImportCraftingTable(EcoCraftDbContext context, Server server, ItemDto craftingTable)
    {
        try
        {
            var dbCraftingTable = CraftingTables.FirstOrDefault(p => p.Name == craftingTable.Name);

            var pluginModules = craftingTable.CraftingTablePluginModules?
                .Select(ctpm => PluginModules.FirstOrDefault(pm => pm.Name == ctpm))
                .Where(pm => pm is not null)
                .ToList() ?? [];

            if (dbCraftingTable is null)
            {
                ImportCraftingTable(
                    context,
                    server,
                    craftingTable.Name,
                    TranslationsToLocalizedField(context, server, craftingTable.LocalizedName),
                    pluginModules
                );
            }
            else
            {
                RefreshCraftingTable(
                    context,
                    dbCraftingTable,
                    TranslationsToLocalizedField(context, server, craftingTable.LocalizedName, dbCraftingTable.LocalizedName),
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

    private ItemOrTag ImportItem(EcoCraftDbContext context, Server server, EcoItemDto item, bool isTag = false)
    {
        var dbItem = ItemOrTags.FirstOrDefault(p => p.Name == item.Name);

        if (dbItem is null)
        {
            dbItem = ImportItemOrTag(context, server, item.Name, TranslationsToLocalizedField(context, server, item.LocalizedName), isTag);
        }
        else
        {
            RefreshItemOrTag(context, dbItem, TranslationsToLocalizedField(context, server, item.LocalizedName, dbItem.LocalizedName), isTag);
        }

        return dbItem;
    }

    private void ImportTags(EcoCraftDbContext context, Server server, List<TagDto> tags)
    {
        var nameOccurence = new Dictionary<string, int>();

        foreach (var tag in tags)
        {
            nameOccurence.Add(tag.Name, 1);

            var dbTag = ImportItem(context, server, tag, true);

            // Diff the M:M instead of bulk-replacing the navigation collection. A bulk reassignment
            // emits per-row DELETE/INSERT on the join table, and any join row that was already
            // removed (e.g. by an earlier cascade in the same SaveChanges batch) makes one of those
            // DELETEs return 0 rows -> DbUpdateConcurrencyException.
            var newAssociatedItems = tag.AssociatedItems
                .Select(i => ItemOrTags.FirstOrDefault(iot => iot.Name == i))
                .Where(e => e is not null)
                .Cast<ItemOrTag>()
                .ToList();

            var newAssociatedSet = new HashSet<ItemOrTag>(newAssociatedItems);
            var oldAssociatedSet = new HashSet<ItemOrTag>(dbTag.AssociatedItems);

            foreach (var item in dbTag.AssociatedItems.ToList())
            {
                if (!newAssociatedSet.Contains(item))
                {
                    dbTag.AssociatedItems.Remove(item);
                }
            }

            foreach (var item in newAssociatedItems)
            {
                if (!oldAssociatedSet.Contains(item))
                {
                    dbTag.AssociatedItems.Add(item);
                }
            }
        }

        foreach (var dbTag in ItemOrTags.Where(iot => iot.IsTag).ToList())
        {
            if (!nameOccurence.TryGetValue(dbTag.Name, out _))
            {
                DeleteItemOrTag(context, dbTag);
            }
        }
    }

    private int ImportRecipes(EcoCraftDbContext context, Server server, List<RecipeDto> recipes, out string[] recipeErrorNames)
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

                var dbRecipe = Recipes.FirstOrDefault(p => p.Name == recipe.Name);
                var dbSkill = Skills.FirstOrDefault(s => s.Name == recipe.RequiredSkill);
                CraftingTable dbCraftingTable;
                try
                {
                    dbCraftingTable = CraftingTables.First(c => c.Name == recipe.CraftingTable);
                }
                catch (Exception e)
                {
                    throw new Exception("Missing CraftingTable: " + recipe.CraftingTable, e);
                }

                if (dbRecipe is null)
                {
                    dbRecipe = ImportRecipe(
                        context,
                        server,
                        recipe.Name,
                        TranslationsToLocalizedField(context, server, recipe.LocalizedName),
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
                    RefreshRecipe(
                        context,
                        dbRecipe,
                        TranslationsToLocalizedField(context, server, recipe.LocalizedName, dbRecipe.LocalizedName),
                        recipe.FamilyName,
                        dbSkill,
                        recipe.RequiredSkillLevel,
                        recipe.IsBlueprint,
                        recipe.IsDefault,
                        dbCraftingTable
                    );
                }

                dbRecipe.Labor = ImportDynamicValue(context, server, dbRecipe.Labor, recipe.Labor);
                dbRecipe.CraftMinutes = ImportDynamicValue(context, server, dbRecipe.CraftMinutes, recipe.CraftMinutes);

                for (var i = 0; i < recipe.Ingredients.Count; i++)
                {
                    recipe.Ingredients[i].Quantity.BaseValue *= -1;
                    recipe.Ingredients[i].Index = i;
                }

                for (var i = 0; i < recipe.Products.Count; i++)
                {
                    recipe.Products[i].Index = i;
                }

                // Snapshot the existing elements; do NOT reassign dbRecipe.Elements.
                // Reassigning a navigation collection whose FK is required+cascade triggers EF
                // to mark every old child as Deleted in the tracker, which then races against
                // the cascade chain at SaveChanges. Instead we diff the collection ourselves
                // and route deletions through DeleteElement (which uses QueueDelete).
                var dbElements = dbRecipe.Elements.ToList();
                var matchedElements = new HashSet<Element>();

                foreach (var element in recipe.Ingredients.Concat(recipe.Products))
                {
                    ItemOrTag dbItemOrTag;

                    try
                    {
                        dbItemOrTag = ItemOrTags.First(e => e.Name == element.ItemOrTag);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Missing ItemOrTag: " + element.ItemOrTag, e);
                    }

                    // element.Quantity * e.Quantity > 0 ensures "element" and "e" are both ingredients or products (You can have an itemOrTag both in ingredient and product => molds,
                    // so we need to be sure dbElement is the correct-retrieved element)
                    // Null guards on e.ItemOrTag / e.Quantity: pre-existing rows can have a dangling
                    // FK to an ItemOrTag from a different server (so it won't be in serverWithData.ItemOrTags
                    // and identity-resolution can't populate the nav). Skipping them here means they
                    // won't match anything new, so the orphan-cleanup loop below will delete them.
                    // Cast to object? to bypass non-nullable annotation — the navs are declared
                    // non-null in the model, but at runtime they really can be null (orphan FK,
                    // unloaded cross-server reference) and we'd crash with NRE without these checks.
                    var dbElement = dbElements.FirstOrDefault(e =>
                        !matchedElements.Contains(e)
                        && (object?)e.ItemOrTag is not null
                        && (object?)e.Quantity is not null
                        && e.ItemOrTag.Name == element.ItemOrTag
                        && element.Quantity.BaseValue * e.Quantity.BaseValue > 0);

                    // Specific for BarrelItem, still need to figure a way to auto calculate this
                    var specificBarrel = element is { ItemOrTag: "BarrelItem", Quantity.BaseValue: > 0, Index: > 0 };

                    if (dbElement is null)
                    {
                        dbElement = ImportElement(
                            context,
                            dbRecipe,
                            dbItemOrTag,
                            element.Index,
                            specificBarrel || (element.Quantity.BaseValue > 0 && recipe.Ingredients.FirstOrDefault(e => e.ItemOrTag == element.ItemOrTag) is not null)
                        );
                    }
                    else
                    {
                        matchedElements.Add(dbElement);
                        RefreshElement(
                            context,
                            dbElement,
                            dbRecipe,
                            dbItemOrTag,
                            element.Index,
                            specificBarrel || (element.Quantity.BaseValue > 0 && recipe.Ingredients.FirstOrDefault(e => e.ItemOrTag == element.ItemOrTag) is not null)
                        );
                    }

                    dbElement.Quantity = ImportDynamicValue(context, server, dbElement.Quantity, element.Quantity);
                }

                foreach (var orphanElement in dbElements.Where(e => !matchedElements.Contains(e)))
                {
                    DeleteElement(context, orphanElement);
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

        foreach (var dbRecipe in Recipes.Where(dbRecipe => !nameOccurence.TryGetValue(dbRecipe.Name, out _)).ToList())
        {
            DeleteRecipe(context, dbRecipe);
        }

        recipeErrorNames = errorNames.ToArray();
        return errorCount;
    }

    private DynamicValue ImportDynamicValue(EcoCraftDbContext context, Server server, DynamicValue? dbDynamicValue, DynamicValueDto newDynamicValue)
    {
        if (dbDynamicValue is null)
        {
            dbDynamicValue = ImportDynamicValue(context, newDynamicValue.BaseValue, server);
        }
        else
        {
            RefreshDynamicValue(context, dbDynamicValue, newDynamicValue.BaseValue);
        }

        dbDynamicValue.HasLayerModifier = newDynamicValue.Modifiers.Any(m => m.DynamicType == "Layer");

        ImportModifiers(context, dbDynamicValue, newDynamicValue.Modifiers);

        return dbDynamicValue;
    }

    private void ImportModifiers(EcoCraftDbContext context, DynamicValue dynamicValue, List<ModifierDto> modifiers)
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
                    iSLinkedToModifier = Skills.SelectMany(s => s.Talents).FirstOrDefault(t => t.Name == modifier.Item);
                    break;
                case "Skill":
                case "Module":
                    iSLinkedToModifier = Skills.FirstOrDefault(t => t.Name == modifier.Item);
                    break;
            }

            if (iSLinkedToModifier is null)
                continue;

            if (dbModifier is null)
            {
                ImportModifier(context, dynamicValue, modifier.DynamicType, modifier.ValueType, iSLinkedToModifier);
            }
            else
            {
                RefreshModifier(context, dbModifier, modifier.DynamicType, modifier.ValueType, iSLinkedToModifier);
            }
        }

        foreach (var dbModifier in dynamicValue.Modifiers.Where(dbModifier => !nameOccurence.TryGetValue(GetModifierName(dbModifier), out _)).ToList())
        {
            DeleteModifier(context, dbModifier);
        }
    }
}
