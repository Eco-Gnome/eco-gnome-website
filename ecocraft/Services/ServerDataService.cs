using ecocraft.Models;
using ecocraft.Services.DbServices;
using System.Security.Cryptography;

namespace ecocraft.Services;

public class ServerDataService(
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

    public string JoinCode { get; private set; } = "";

    public async Task RetrieveServerData(Server? server)
    {
        if (server is null)
        {
            Skills = [];
            CraftingTables = [];
            PluginModules = [];
            Recipes = [];
            ItemOrTags = [];
            JoinCode = "";

            return;
        }

        var skillsTask = skillDbService.GetByServerAsync(server);
        var craftingTablesTask = craftingTableDbService.GetByServerAsync(server);
        var pluginModulesTask = pluginModuleDbService.GetByServerAsync(server);
        var recipesTask = recipeDbService.GetByServerAsync(server);
        var itemOrTagsTask = itemOrTagDbService.GetByServerAsync(server);

        await Task.WhenAll(skillsTask, craftingTablesTask, recipesTask, itemOrTagsTask, pluginModulesTask);

        Skills = skillsTask.Result;
        CraftingTables = craftingTablesTask.Result;
        PluginModules = pluginModulesTask.Result;
        Recipes = recipesTask.Result;
        ItemOrTags = itemOrTagsTask.Result;
        JoinCode = server.JoinCode;
    }

    public Skill ImportSkill(Server server, string name, LocalizedField localizedName, string? profession, float[] laborReducePercent)
    {
        var skill = new Skill
        {
            Name = name,
            Profession = profession,
            LaborReducePercent = laborReducePercent,
            Server = server,
            LocalizedName = localizedName,
        };

        Skills.Add(skill);
        skillDbService.Add(skill);

        return skill;
    }

    public void RefreshSkill(Skill skill, LocalizedField localizedName, string? profession, float[] laborReducePercent)
    {
        skill.LocalizedName = localizedName;
        skill.Profession = profession;
        skill.LaborReducePercent = laborReducePercent;

        skillDbService.Update(skill);
    }

    public PluginModule ImportPluginModule(Server server, string name, LocalizedField localizedName, float percent)
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

    public void RefreshPluginModule(PluginModule pluginModule, LocalizedField localizedName, float percent)
    {
        pluginModule.LocalizedName = localizedName;
        pluginModule.Percent = percent;

        pluginModuleDbService.Update(pluginModule);
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

        // Specific for ItemOrTag, they  can appear in multiple recipes, so we must not update them if they have not yet been created in the database
        if (!ItemOrTags.Contains(itemOrTag))
        {
            itemOrTagDbService.Update(itemOrTag);
        }
    }

    public Recipe ImportRecipe(Server server, string name, LocalizedField localizedName, string familyName, float craftMinutes, Skill? skill,
        int requiredSkillLevel, bool isBlueprint, bool isDefault, float labor, CraftingTable craftingTable)
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

    public void RefreshRecipe(Recipe recipe, LocalizedField localizedName, string familyName, float craftMinutes, Skill? skill, int requiredSkillLevel,
        bool isBlueprint, bool isDefault, float labor, CraftingTable craftingTable)
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

    public Element ImportElement(Recipe recipe, ItemOrTag itemOrTag, int index, float quantity, bool isDynamic, Skill? skill, bool lavishTalent)
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
        };

        elementDbService.Add(element);

        return element;
    }

    public void RefreshElement(Element element, Recipe recipe, ItemOrTag itemOrTag, int index, float quantity, bool isDynamic, Skill? skill, bool lavishTalent)
    {
        element.Recipe = recipe;
        element.ItemOrTag = itemOrTag;
        element.Index = index;
        element.Quantity = quantity;
        element.IsDynamic = isDynamic;
        element.Skill = skill;
        element.LavishTalent = lavishTalent;

        elementDbService.Update(element);
    }
}
