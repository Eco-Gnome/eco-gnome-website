using ecocraft.Models;
using ICSharpCode.Decompiler.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.ImportData;

public partial class ImportDataService
{
    // Detach the entity from EF's change tracker, then queue a SQL DELETE for after SaveChanges.
    // Detach prevents EF from emitting an UPDATE/DELETE for the entity inside SaveChanges, which
    // would otherwise race against the DB-level cascade triggered by the queued delete and throw
    // DbUpdateConcurrencyException on 0 rows affected. See EcoCraftDbContext.ContextSaveAsync.
    private static void DetachAndQueueDelete<T>(EcoCraftDbContext context, T entity, Guid id) where T : class
    {
        var entry = context.Entry(entity);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
        context.QueueDelete<T>(id);
    }

    private Skill ImportSkill(EcoCraftDbContext context, Server server, string name, LocalizedField localizedName, string? profession, int maxLevel, decimal[] laborReducePercent)
    {
        var skill = new Skill
        {
            Name = name,
            LocalizedName = localizedName,
            Profession = profession,
            MaxLevel = maxLevel,
            LaborReducePercent = laborReducePercent,
            Server = server,
        };

        Skills.Add(skill);
        context.Skills.Add(skill);

        return skill;
    }

    private void RefreshSkill(EcoCraftDbContext context, Skill skill, LocalizedField localizedName, string? profession, int maxLevel, decimal[] laborReducePercent)
    {
        skill.LocalizedName = localizedName;
        skill.Profession = profession;
        skill.MaxLevel = maxLevel;
        skill.LaborReducePercent = laborReducePercent;

        context.Skills.Update(skill);
    }

    private void DeleteSkill(EcoCraftDbContext context, Skill skill)
    {
        Skills.Remove(skill);
        DetachAndQueueDelete(context, skill, skill.Id);
    }

    private Talent ImportTalent(EcoCraftDbContext context, Skill skill, string name, LocalizedField localizedName, LocalizedField localizedDescription, string talentGroupName, int level, int maxLevel, List<TalentBonusDto> bonuses)
    {
        var talent = new Talent
        {
            Skill = skill,
            Name = name,
            LocalizedName = localizedName,
            LocalizedDescription = localizedDescription,
            TalentGroupName = talentGroupName,
            Level = level,
            MaxLevel = maxLevel,
        };

        context.Talents.Add(talent);

        ReplaceTalentBonuses(context, talent, bonuses);

        return talent;
    }

    private void RefreshTalent(EcoCraftDbContext context, Talent talent, Skill skill, LocalizedField localizedName, LocalizedField localizedDescription, string talentGroupName, int level, int maxLevel, List<TalentBonusDto> bonuses)
    {
        talent.Skill = skill;
        talent.LocalizedName = localizedName;
        talent.LocalizedDescription = localizedDescription;
        talent.TalentGroupName = talentGroupName;
        talent.Level = level;
        talent.MaxLevel = maxLevel;

        context.Talents.Update(talent);

        ReplaceTalentBonuses(context, talent, bonuses);
    }

    private void ReplaceTalentBonuses(EcoCraftDbContext context, Talent talent, List<TalentBonusDto> bonuses)
    {
        foreach (var existing in talent.Bonuses.ToList())
        {
            DetachAndQueueDelete(context, existing, existing.Id);
        }
        talent.Bonuses.Clear();

        foreach (var bonusDto in bonuses)
        {
            var bonus = new TalentBonus
            {
                Talent = talent,
                Action = bonusDto.Action,
                EffectType = bonusDto.EffectType,
                Value = bonusDto.Value,
                Cap = bonusDto.Cap,
                ItemTags = bonusDto.ItemTags,
            };

            talent.Bonuses.Add(bonus);
            context.TalentBonuses.Add(bonus);
        }
    }

    private void DeleteTalent(EcoCraftDbContext context, Talent talent)
    {
        talent.Skill.Talents.Remove(talent);
        DetachAndQueueDelete(context, talent, talent.Id);
    }

    private PluginModule ImportPluginModule(EcoCraftDbContext context, Server server, string name, LocalizedField localizedName, PluginType pluginType, decimal percent, Skill? skill, decimal? skillPercent)
    {
        var pluginModule = new PluginModule
        {
            Name = name,
            LocalizedName = localizedName,
            PluginType = pluginType,
            Percent = percent,
            Skill = skill,
            SkillPercent = skillPercent,
            Server = server,
        };

        PluginModules.Add(pluginModule);
        context.PluginModules.Add(pluginModule);

        return pluginModule;
    }

    private void RefreshPluginModule(EcoCraftDbContext context, PluginModule pluginModule, LocalizedField localizedName, PluginType pluginType, decimal percent, Skill? skill, decimal? skillPercent)
    {
        pluginModule.LocalizedName = localizedName;
        pluginModule.PluginType = pluginType;
        pluginModule.Percent = percent;
        pluginModule.Skill = skill;
        pluginModule.SkillPercent = skillPercent;

        context.PluginModules.Update(pluginModule);
    }

    private void DeletePluginModule(EcoCraftDbContext context, PluginModule pluginModule)
    {
        PluginModules.Remove(pluginModule);
        DetachAndQueueDelete(context, pluginModule, pluginModule.Id);
    }

    private CraftingTable ImportCraftingTable(EcoCraftDbContext context, Server server, string name, LocalizedField localizedName, List<PluginModule> pluginModules)
    {
        var craftingTable = new CraftingTable
        {
            Name = name,
            PluginModules = pluginModules,
            Server = server,
            LocalizedName = localizedName,
        };

        CraftingTables.Add(craftingTable);
        context.CraftingTables.Add(craftingTable);

        return craftingTable;
    }

    private void RefreshCraftingTable(EcoCraftDbContext context, CraftingTable craftingTable, LocalizedField localizedName, List<PluginModule> pluginModules)
    {
        craftingTable.LocalizedName = localizedName;

        // Diff the M:M instead of bulk-replacing the collection: see ImportTags for full rationale.
        var newPluginModuleSet = new HashSet<PluginModule>(pluginModules);
        var oldPluginModuleSet = new HashSet<PluginModule>(craftingTable.PluginModules);

        foreach (var pluginModule in craftingTable.PluginModules.ToList())
        {
            if (!newPluginModuleSet.Contains(pluginModule))
            {
                craftingTable.PluginModules.Remove(pluginModule);
            }
        }

        foreach (var pluginModule in pluginModules)
        {
            if (!oldPluginModuleSet.Contains(pluginModule))
            {
                craftingTable.PluginModules.Add(pluginModule);
            }
        }

        context.CraftingTables.Update(craftingTable);
    }

    private void DeleteCraftingTable(EcoCraftDbContext context, CraftingTable craftingTable)
    {
        CraftingTables.Remove(craftingTable);
        DetachAndQueueDelete(context, craftingTable, craftingTable.Id);
    }

    private ItemOrTag ImportItemOrTag(EcoCraftDbContext context, Server server, string name, LocalizedField localizedName, bool isTag)
    {
        var itemOrTag = new ItemOrTag
        {
            Name = name,
            Server = server,
            IsTag = isTag,
            LocalizedName = localizedName,
        };

        ItemOrTags.Add(itemOrTag);
        context.ItemOrTags.Add(itemOrTag);

        return itemOrTag;
    }

    private void RefreshItemOrTag(EcoCraftDbContext context, ItemOrTag itemOrTag, LocalizedField localizedName, bool isTag)
    {
        itemOrTag.LocalizedName = localizedName;
        itemOrTag.IsTag = isTag;

        context.ItemOrTags.Update(itemOrTag);
    }

    private void DeleteItemOrTag(EcoCraftDbContext context, ItemOrTag itemOrTag)
    {
        ItemOrTags.Remove(itemOrTag);
        DetachAndQueueDelete(context, itemOrTag, itemOrTag.Id);
    }

    private static void ApplyExportedItemFields(ItemOrTag dbItem, ItemDto item)
    {
        dbItem.FuelCalories = item.FuelCalories;
        dbItem.FuelConsumptionPerSecond = item.FuelConsumptionPerSecond;
        dbItem.AcceptedFuelTags = item.AcceptedFuelTags;

        dbItem.FoodCalories = item.Food?.Calories;
        dbItem.FoodCarbs = item.Food?.Carbs;
        dbItem.FoodProtein = item.Food?.Protein;
        dbItem.FoodFat = item.Food?.Fat;
        dbItem.FoodVitamins = item.Food?.Vitamins;

        dbItem.HousingRoomCategory = item.Housing?.RoomCategory;
        dbItem.HousingBaseValue = item.Housing?.BaseValue;
        dbItem.HousingTypeForRoomLimit = item.Housing?.TypeForRoomLimit;
        dbItem.HousingDiminishingReturnMultiplier = item.Housing?.DiminishingReturnMultiplier;
        dbItem.HousingDiminishingMultiplierAcrossFullProperty = item.Housing?.DiminishingMultiplierAcrossFullProperty;
    }

    private Recipe ImportRecipe(EcoCraftDbContext context, Server server, string name, LocalizedField localizedName, string familyName, Skill? skill, int requiredSkillLevel, bool isBlueprint, bool isDefault, CraftingTable craftingTable)
    {
        var recipe = new Recipe
        {
            Name = name,
            LocalizedName = localizedName,
            FamilyName = familyName,
            Skill = skill,
            SkillLevel = requiredSkillLevel,
            IsBlueprint = isBlueprint,
            IsDefault = isDefault,
            CraftingTable = craftingTable,
            Server = server,
        };

        Recipes.Add(recipe);
        context.Recipes.Add(recipe);

        return recipe;
    }

    private void RefreshRecipe(EcoCraftDbContext context, Recipe recipe, LocalizedField localizedName, string familyName, Skill? skill, int requiredSkillLevel, bool isBlueprint, bool isDefault, CraftingTable craftingTable)
    {
        recipe.LocalizedName = localizedName;
        recipe.FamilyName = familyName;
        recipe.Skill = skill;
        recipe.SkillLevel = requiredSkillLevel;
        recipe.IsBlueprint = isBlueprint;
        recipe.IsDefault = isDefault;
        recipe.CraftingTable = craftingTable;

        context.Recipes.Update(recipe);
    }

    private void DeleteRecipe(EcoCraftDbContext context, Recipe recipe)
    {
        Recipes.Remove(recipe);
        DetachAndQueueDelete(context, recipe, recipe.Id);
    }

    private DynamicValue ImportDynamicValue(EcoCraftDbContext context, decimal baseValue, Server server)
    {
        var dynamicValue = new DynamicValue
        {
            BaseValue = baseValue,
            Server = server
        };

        context.DynamicValues.Add(dynamicValue);

        return dynamicValue;
    }

    private void RefreshDynamicValue(EcoCraftDbContext context, DynamicValue dynamicValue, decimal baseValue)
    {
        dynamicValue.BaseValue = baseValue;
        context.DynamicValues.Update(dynamicValue);
    }

    private void DeleteDynamicValue(EcoCraftDbContext context, DynamicValue dynamicValue)
    {
        DetachAndQueueDelete(context, dynamicValue, dynamicValue.Id);
    }

    private Modifier ImportModifier(EcoCraftDbContext context, DynamicValue dynamicValue, string dynamicType, string valueType, ISLinkedToModifier iSLinkedToModifier)
    {
        var modifier = new Modifier
        {
            DynamicValue = dynamicValue,
            DynamicType = dynamicType,
            ValueType = valueType,
        };

        switch (iSLinkedToModifier)
        {
            case Skill skill:
                modifier.Skill = skill;
                break;
            case Talent talent:
                modifier.Talent = talent;
                break;
        }

        context.Modifiers.Add(modifier);

        return modifier;
    }

    private void RefreshModifier(EcoCraftDbContext context, Modifier modifier, string dynamicType, string valueType, ISLinkedToModifier iSLinkedToModifier)
    {
        modifier.DynamicType = dynamicType;
        modifier.ValueType = valueType;

        switch (iSLinkedToModifier)
        {
            case Skill skill:
                modifier.Talent = null;
                modifier.Skill = skill;
                break;
            case Talent talent:
                modifier.Talent = talent;
                modifier.Skill = null;
                break;
        }

        context.Modifiers.Update(modifier);
    }

    private void DeleteModifier(EcoCraftDbContext context, Modifier modifier)
    {
        modifier.DynamicValue.Modifiers.Remove(modifier);
        DetachAndQueueDelete(context, modifier, modifier.Id);
    }

    private Element ImportElement(EcoCraftDbContext context, Recipe recipe, ItemOrTag itemOrTag, int index, bool shouldReintegrate)
    {
        var element = new Element
        {
            Recipe = recipe,
            ItemOrTag = itemOrTag,
            Index = index,
            DefaultShare = 0,
            DefaultIsReintegrated = shouldReintegrate
        };

        context.Elements.Add(element);

        return element;
    }

    private void RefreshElement(EcoCraftDbContext context, Element element, Recipe recipe, ItemOrTag itemOrTag, int index, bool shouldReintegrate)
    {
        element.Recipe = recipe;
        element.ItemOrTag = itemOrTag;
        element.Index = index;
        element.DefaultShare = 0;
        element.DefaultIsReintegrated = shouldReintegrate;

        context.Elements.Update(element);
    }

    private void DeleteElement(EcoCraftDbContext context, Element element)
    {
        element.Recipe.Elements.Remove(element);
        DetachAndQueueDelete(context, element, element.Id);
    }
}
