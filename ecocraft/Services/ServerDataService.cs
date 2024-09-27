using ecocraft.Models;

namespace ecocraft.Services;

public class ServerDataService(SkillDbService skillDbService,
    CraftingTableDbService craftingTableDbService,
    RecipeDbService recipeDbService,
    ItemOrTagDbService itemOrTagDbService,
    PluginModuleDbService pluginModuleDbService)
{
    public List<Skill> Skills { get; private set; } = [];
    public List<PluginModule> PluginModules { get; private set; } = [];
    public List<CraftingTable> CraftingTables { get; private set; } = [];
    public List<Recipe> Recipes { get; private set; } = [];
    public List<ItemOrTag> ItemOrTags { get; private set; } = [];

    public async Task RetrieveServerData(Server server)
    {
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
    }

}