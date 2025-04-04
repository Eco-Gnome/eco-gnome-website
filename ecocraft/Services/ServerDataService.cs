﻿using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services;

public class ServerDataService(
    EcoCraftDbContext dbContext,
    SkillDbService skillDbService,
    CraftingTableDbService craftingTableDbService,
    RecipeDbService recipeDbService,
    ItemOrTagDbService itemOrTagDbService,
    PluginModuleDbService pluginModuleDbService,
    ElementDbService elementDbService)
{
    public List<Skill> Skills { get; private set; } = [];
    public List<PluginModule> PluginModules { get; private set; } = [];
    public List<CraftingTable> CraftingTables { get; private set; } = [];
    public List<Recipe> Recipes { get; private set; } = [];
    public List<ItemOrTag> ItemOrTags { get; private set; } = [];

    public async Task RetrieveServerData(Server? server)
    {
        if (server is null)
        {
            Skills = [];
            CraftingTables = [];
            PluginModules = [];
            Recipes = [];
            ItemOrTags = [];

            return;
        }

        var data = await GetServerData(server);

        Skills = data.skills;
        CraftingTables = data.craftingTables;
        PluginModules = data.pluginModules;
        Recipes = data.recipes;
        ItemOrTags = data.itemOrTags;
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

    public Skill ImportSkill(Server server, string name, LocalizedField localizedName, string? profession, decimal[] laborReducePercent, decimal? lavishTalentValue)
    {
        var skill = new Skill
        {
            Name = name,
            Profession = profession,
            LaborReducePercent = laborReducePercent,
            Server = server,
            LocalizedName = localizedName,
            LavishTalentValue = lavishTalentValue
        };

        Skills.Add(skill);
        skillDbService.Add(skill);

        return skill;
    }

    public void RefreshSkill(Skill skill, LocalizedField localizedName, string? profession, decimal[] laborReducePercent, decimal? lavishTalentValue)
    {
        skill.LocalizedName = localizedName;
        skill.Profession = profession;
        skill.LaborReducePercent = laborReducePercent;
        skill.LavishTalentValue = lavishTalentValue;

        skillDbService.Update(skill);
    }

    public void DeleteSkill(Skill skill)
    {
        skillDbService.Delete(skill);
    }

    public PluginModule ImportPluginModule(Server server, string name, LocalizedField localizedName, decimal percent)
    {
        var pluginModule = new PluginModule
        {
            Name = name,
            Percent = percent,
            LocalizedName = localizedName,
            Server = server,
        };

        PluginModules.Add(pluginModule);
        pluginModuleDbService.Add(pluginModule);

        return pluginModule;
    }

    public void RefreshPluginModule(PluginModule pluginModule, LocalizedField localizedName, decimal percent)
    {
        pluginModule.LocalizedName = localizedName;
        pluginModule.Percent = percent;

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

    public Recipe ImportRecipe(Server server, string name, LocalizedField localizedName, string familyName, decimal craftMinutes, Skill? skill,
        int requiredSkillLevel, bool isBlueprint, bool isDefault, decimal labor, CraftingTable craftingTable)
    {
        var recipe = new Recipe
        {
            Name = name,
            LocalizedName = localizedName,
            FamilyName = familyName,
            CraftMinutes = craftMinutes,
            Skill = skill,
            SkillLevel = requiredSkillLevel,
            IsBlueprint = isBlueprint,
            IsDefault = isDefault,
            Labor = labor,
            CraftingTable = craftingTable,
            Server = server,
        };

        Recipes.Add(recipe);
        recipeDbService.Add(recipe);

        return recipe;
    }

    public void RefreshRecipe(Recipe recipe, LocalizedField localizedName, string familyName, decimal craftMinutes, Skill? skill, int requiredSkillLevel,
        bool isBlueprint, bool isDefault, decimal labor, CraftingTable craftingTable)
    {
        recipe.LocalizedName = localizedName;
        recipe.FamilyName = familyName;
        recipe.CraftMinutes = craftMinutes;
        recipe.Skill = skill;
        recipe.SkillLevel = requiredSkillLevel;
        recipe.IsBlueprint = isBlueprint;
        recipe.IsDefault = isDefault;
        recipe.Labor = labor;
        recipe.CraftingTable = craftingTable;

        recipeDbService.Update(recipe);
    }

    public void DeleteRecipe(Recipe recipe)
    {
        recipeDbService.Delete(recipe);
    }

    public Element ImportElement(Recipe recipe, ItemOrTag itemOrTag, int index, decimal quantity, bool isDynamic, Skill? skill, bool lavishTalent, bool shouldReintegrate)
    {
        var element = new Element
        {
            Recipe = recipe,
            ItemOrTag = itemOrTag,
            Index = index,
            Quantity = quantity,
            IsDynamic = isDynamic,
            Skill = skill,
            LavishTalent = lavishTalent,
            DefaultShare = 0,
            DefaultIsReintegrated = shouldReintegrate
        };

        elementDbService.Add(element);

        return element;
    }

    public void RefreshElement(Element element, Recipe recipe, ItemOrTag itemOrTag, int index, decimal quantity, bool isDynamic, Skill? skill, bool lavishTalent, bool shouldReintegrate)
    {
        element.Recipe = recipe;
        element.ItemOrTag = itemOrTag;
        element.Index = index;
        element.Quantity = quantity;
        element.IsDynamic = isDynamic;
        element.Skill = skill;
        element.LavishTalent = lavishTalent;
        element.DefaultShare = 0;
        element.DefaultIsReintegrated = shouldReintegrate;

        elementDbService.Update(element);
    }
}
