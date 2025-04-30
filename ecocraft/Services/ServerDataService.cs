using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services;

public class ServerDataService(
    EcoCraftDbContext dbContext,
    SkillDbService skillDbService,
    CraftingTableDbService craftingTableDbService,
    RecipeDbService recipeDbService,
    ItemOrTagDbService itemOrTagDbService,
    PluginModuleDbService pluginModuleDbService,
    TalentDbService talentDbService,
    DynamicValueDbService dynamicValueDbService,
    ModifierDbService modifierDbService,
    ElementDbService elementDbService)
{
    public bool IsDataRetrieved  { get; private set; }

    public List<Skill> Skills { get; private set; } = [];
    public List<PluginModule> PluginModules { get; private set; } = [];
    public List<CraftingTable> CraftingTables { get; private set; } = [];
    public List<Recipe> Recipes { get; private set; } = [];
    public List<ItemOrTag> ItemOrTags { get; private set; } = [];

    public async Task RetrieveServerData(Server? server, bool force = false)
    {
        if (server is null)
        {
            Skills = [];
            CraftingTables = [];
            PluginModules = [];
            Recipes = [];
            ItemOrTags = [];

            IsDataRetrieved = false;

            return;
        }

        if (IsDataRetrieved && !force) return;

        var data = await GetServerData(server);

        Skills = data.skills;
        CraftingTables = data.craftingTables;
        PluginModules = data.pluginModules;
        Recipes = data.recipes;
        ItemOrTags = data.itemOrTags;

        IsDataRetrieved = true;
    }

    public async Task<(List<Skill> skills, List<CraftingTable> craftingTables, List<PluginModule> pluginModules, List<Recipe> recipes, List<ItemOrTag> itemOrTags)> GetServerData(Server server)
    {
        var skillsTask = skillDbService.GetByServerAsync(server);
        var craftingTablesTask = craftingTableDbService.GetByServerAsync(server);
        var recipesTask = recipeDbService.GetByServerAsync(server);
        var itemOrTagsTask = itemOrTagDbService.GetByServerAsync(server);
        var pluginModulesTask = pluginModuleDbService.GetByServerAsync(server);

        await Task.WhenAll(skillsTask, craftingTablesTask, recipesTask, itemOrTagsTask, pluginModulesTask);

        return (skillsTask.Result, craftingTablesTask.Result, pluginModulesTask.Result, recipesTask.Result, itemOrTagsTask.Result);
    }

    public async Task CopyServerContribution(Server copyServer)
    {
        var data = await itemOrTagDbService.GetByServerAsync(copyServer);

        foreach (var item in ItemOrTags)
        {
            var copyItem = data.FirstOrDefault(i => i.Name == item.Name);

            if (copyItem is not null)
            {
                item.MinPrice = copyItem.MinPrice;
                item.DefaultPrice = copyItem.DefaultPrice;
                item.MaxPrice = copyItem.MaxPrice;
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public void Dissociate(Server server)
    {
        server.EcoServerId = null;
    }

    public Skill ImportSkill(Server server, string name, LocalizedField localizedName, string? profession, int maxLevel, decimal[] laborReducePercent)
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
        skillDbService.Add(skill);

        return skill;
    }

    public void RefreshSkill(Skill skill, LocalizedField localizedName, string? profession, int maxLevel, decimal[] laborReducePercent)
    {
        skill.LocalizedName = localizedName;
        skill.Profession = profession;
        skill.MaxLevel = maxLevel;
        skill.LaborReducePercent = laborReducePercent;

        skillDbService.Update(skill);
    }

    public void DeleteSkill(Skill skill)
    {
        skillDbService.Delete(skill);
    }

    public Talent ImportTalent(Skill skill, string name, LocalizedField localizedName, string talentGroupName, int level, decimal value)
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

        talentDbService.Add(talent);

        return talent;
    }

    public void RefreshTalent(Talent talent, Skill skill, LocalizedField localizedName, string talentGroupName, int level, decimal value)
    {
        talent.Skill = skill;
        talent.LocalizedName = localizedName;
        talent.TalentGroupName = talentGroupName;
        talent.Level = level;
        talent.Value = value;

        talentDbService.Update(talent);
    }

    public void DeleteTalent(Talent talent)
    {
        talentDbService.Delete(talent);
    }

    public PluginModule ImportPluginModule(Server server, string name, LocalizedField localizedName, PluginType pluginType, decimal percent, Skill? skill, decimal? skillPercent)
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
        pluginModuleDbService.Add(pluginModule);

        return pluginModule;
    }

    public void RefreshPluginModule(PluginModule pluginModule, LocalizedField localizedName, PluginType pluginType, decimal percent, Skill? skill, decimal? skillPercent)
    {
        pluginModule.LocalizedName = localizedName;
        pluginModule.PluginType = pluginType;
        pluginModule.Percent = percent;
        pluginModule.Skill = skill;
        pluginModule.SkillPercent = skillPercent;

        pluginModuleDbService.Update(pluginModule);
    }

    public void DeletePluginModule(PluginModule pluginModule)
    {
        pluginModuleDbService.Delete(pluginModule);
    }

    public CraftingTable ImportCraftingTable(Server server, string name, LocalizedField localizedName, List<PluginModule> pluginModules)
    {
        var craftingTable = new CraftingTable
        {
            Name = name,
            PluginModules = pluginModules,
            Server = server,
            LocalizedName = localizedName,
        };

        CraftingTables.Add(craftingTable);
        craftingTableDbService.Add(craftingTable);

        return craftingTable;
    }

    public void RefreshCraftingTable(CraftingTable craftingTable, LocalizedField localizedName, List<PluginModule> pluginModules)
    {
        craftingTable.LocalizedName = localizedName;
        craftingTable.PluginModules = pluginModules;

        craftingTableDbService.Update(craftingTable);
    }

    public void DeleteCraftingTable(CraftingTable craftingTable)
    {
        craftingTableDbService.Delete(craftingTable);
    }

    public ItemOrTag ImportItemOrTag(Server server, string name, LocalizedField localizedName, bool isTag)
    {
        var itemOrTag = new ItemOrTag
        {
            Name = name,
            Server = server,
            IsTag = isTag,
            LocalizedName = localizedName,
        };

        ItemOrTags.Add(itemOrTag);
        itemOrTagDbService.Add(itemOrTag);

        return itemOrTag;
    }

    public void RefreshItemOrTag(ItemOrTag itemOrTag, LocalizedField localizedName, bool isTag)
    {
        itemOrTag.LocalizedName = localizedName;
        itemOrTag.IsTag = isTag;

        itemOrTagDbService.Update(itemOrTag);
    }

    public void DeleteItemOrTag(ItemOrTag itemOrTag)
    {
        itemOrTagDbService.Delete(itemOrTag);
    }

    public Recipe ImportRecipe(Server server, string name, LocalizedField localizedName, string familyName, Skill? skill,
        int requiredSkillLevel, bool isBlueprint, bool isDefault, CraftingTable craftingTable)
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
        recipeDbService.Add(recipe);

        return recipe;
    }

    public void RefreshRecipe(Recipe recipe, LocalizedField localizedName, string familyName, Skill? skill, int requiredSkillLevel,
        bool isBlueprint, bool isDefault, CraftingTable craftingTable)
    {
        recipe.LocalizedName = localizedName;
        recipe.FamilyName = familyName;
        recipe.Skill = skill;
        recipe.SkillLevel = requiredSkillLevel;
        recipe.IsBlueprint = isBlueprint;
        recipe.IsDefault = isDefault;
        recipe.CraftingTable = craftingTable;

        recipeDbService.Update(recipe);
    }

    public void DeleteRecipe(Recipe recipe)
    {
        recipeDbService.Delete(recipe);
    }

    public DynamicValue ImportDynamicValue(decimal baseValue, Server server)
    {
        var dynamicValue = new DynamicValue
        {
            BaseValue = baseValue,
            Server = server
        };

        dynamicValueDbService.Add(dynamicValue);

        return dynamicValue;
    }

    public void RefreshDynamicValue(DynamicValue dynamicValue, decimal baseValue)
    {
        dynamicValue.BaseValue = baseValue;
        dynamicValueDbService.Update(dynamicValue);
    }

    public void DeleteDynamicValue(DynamicValue dynamicValue)
    {
        dynamicValueDbService.Delete(dynamicValue);
    }

    public Modifier ImportModifier(DynamicValue dynamicValue, string dynamicType, ISLinkedToModifier iSLinkedToModifier)
    {
        var modifier = new Modifier
        {
            DynamicValue = dynamicValue,
            DynamicType = dynamicType,
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

        modifierDbService.Add(modifier);

        return modifier;
    }

    public void RefreshModifier(Modifier modifier, string dynamicType, ISLinkedToModifier iSLinkedToModifier)
    {
        modifier.DynamicType = dynamicType;

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

        modifierDbService.Update(modifier);
    }

    public void DeleteModifier(Modifier modifier)
    {
        modifierDbService.Delete(modifier);
    }

    public Element ImportElement(Recipe recipe, ItemOrTag itemOrTag, int index, bool shouldReintegrate)
    {
        var element = new Element
        {
            Recipe = recipe,
            ItemOrTag = itemOrTag,
            Index = index,
            DefaultShare = 0,
            DefaultIsReintegrated = shouldReintegrate
        };

        elementDbService.Add(element);

        return element;
    }

    public void RefreshElement(Element element, Recipe recipe, ItemOrTag itemOrTag, int index, bool shouldReintegrate)
    {
        element.Recipe = recipe;
        element.ItemOrTag = itemOrTag;
        element.Index = index;
        element.DefaultShare = 0;
        element.DefaultIsReintegrated = shouldReintegrate;

        elementDbService.Update(element);
    }

    public void DeleteElement(Element element)
    {
        elementDbService.Delete(element);
    }
}
