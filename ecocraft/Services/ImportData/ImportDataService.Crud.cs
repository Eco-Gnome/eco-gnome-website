using ecocraft.Models;
using ICSharpCode.Decompiler.CSharp.Syntax;

namespace ecocraft.Services.ImportData;

public partial class ImportDataService
{
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
        context.Skills.Remove(skill);
    }

    private Talent ImportTalent(EcoCraftDbContext context, Skill skill, string name, LocalizedField localizedName, string talentGroupName, int level, decimal value)
    {
        var talent = new Talent
        {
            Skill = skill,
            Name = name,
            LocalizedName = localizedName,
            TalentGroupName = talentGroupName,
            Level = level,
            Value = value,
        };

        context.Talents.Add(talent);

        return talent;
    }

    private void RefreshTalent(EcoCraftDbContext context, Talent talent, Skill skill, LocalizedField localizedName, string talentGroupName, int level, decimal value)
    {
        talent.Skill = skill;
        talent.LocalizedName = localizedName;
        talent.TalentGroupName = talentGroupName;
        talent.Level = level;
        talent.Value = value;

        context.Talents.Update(talent);
    }

    private void DeleteTalent(EcoCraftDbContext context, Talent talent)
    {
        talent.Skill.Talents.Remove(talent);
        context.Talents.Remove(talent);
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
        context.PluginModules.Remove(pluginModule);
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
        craftingTable.PluginModules = pluginModules;

        context.CraftingTables.Update(craftingTable);
    }

    private void DeleteCraftingTable(EcoCraftDbContext context, CraftingTable craftingTable)
    {
        CraftingTables.Remove(craftingTable);
        context.CraftingTables.Remove(craftingTable);
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
        context.ItemOrTags.Remove(itemOrTag);
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
        context.Recipes.Remove(recipe);
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
        context.DynamicValues.Remove(dynamicValue);
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
        context.Modifiers.Remove(modifier);
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
        context.Elements.Remove(element);
    }
}
