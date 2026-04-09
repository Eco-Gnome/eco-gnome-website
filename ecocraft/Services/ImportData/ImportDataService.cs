using System.Text.Json;
using System.Text.Json.Serialization;
using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.ImportData;

public class ImportException(string? message) : Exception(message);

public partial class ImportDataService(
    IDbContextFactory<EcoCraftDbContext> factory,
    LocalizationService localizationService,
    ServerDbService serverDbService,
    ServerDataService serverDataService)
{
    private const int SupportedVersion = 2;

    private List<Skill> Skills { get; set; } = [];
    private List<PluginModule> PluginModules { get; set; } = [];
    private List<CraftingTable> CraftingTables { get; set; } = [];
    private List<Recipe> Recipes { get; set; } = [];
    private List<ItemOrTag> ItemOrTags { get; set; } = [];

    private void SetTrackedCollectionsFromServer(Server serverWithData)
    {
        Skills = serverWithData.Skills;
        PluginModules = serverWithData.PluginModules;
        CraftingTables = serverWithData.CraftingTables;
        Recipes = serverWithData.Recipes;
        ItemOrTags = serverWithData.ItemOrTags;
    }

    public async Task<(int, string[])> ImportServerData(string jsonContent, Server server)
    {
        var errorCount = 0;
        string[] itemErrorNames = [];
        string[] recipeErrorNames = [];

        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            var serverWithData = await serverDbService.GetServerWithData(server.Id, context);
            context.Attach(serverWithData);
            SetTrackedCollectionsFromServer(serverWithData);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new LanguageCodeDictionaryConverter());

            ImportDataDto? importedData;

            try
            {
                importedData = JsonSerializer.Deserialize<ImportDataDto>(jsonContent, options);
            }
            catch (Exception e)
            {
                throw new ImportException("No data / Wrong file format: " + e.Message);
            }

            if (importedData is null) throw new ImportException("No data / Wrong file format");

            if (importedData.Version != SupportedVersion) throw new ImportException(localizationService.GetTranslation("ServerManagement.Snackbar.UploadWrongVersion", SupportedVersion.ToString()));

            ImportSkills(context, serverWithData, importedData.Skills);
            errorCount += ImportItems(context, serverWithData, importedData.Items, out itemErrorNames);
            ImportTags(context, serverWithData, importedData.Tags);
            errorCount += ImportRecipes(context, serverWithData, importedData.Recipes, out recipeErrorNames);
            serverWithData.LastDataUploadTime = DateTimeOffset.UtcNow;
        });

        return (errorCount, itemErrorNames.Concat(recipeErrorNames).ToArray());
    }

    public async Task CopyServerData(Server copyServer, Server targetServer)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            var data = await GetServerDataAsDto(context, copyServer);
            var targetServerWithData = await serverDbService.GetServerWithData(targetServer.Id, context);
            context.Attach(targetServerWithData);
            SetTrackedCollectionsFromServer(targetServerWithData);

            ImportSkills(context, targetServerWithData, data.Skills);
            ImportItems(context, targetServerWithData, data.Items, out _);
            ImportTags(context, targetServerWithData, data.Tags);
            ImportRecipes(context, targetServerWithData, data.Recipes, out _);

            targetServerWithData.LastDataUploadTime = DateTimeOffset.UtcNow;
        });
    }
}
