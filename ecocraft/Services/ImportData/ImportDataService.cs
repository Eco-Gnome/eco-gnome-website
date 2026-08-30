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
    // v4 : données de prix ; v5 : + données « bâtiment » (occupancy, tiers, configs) pour le planificateur.
    // Un export v4 s'importe toujours, le planificateur est alors indisponible sur le serveur (HasBuildingData = false).
    private static readonly int[] SupportedVersions = [4, 5];
    private const int ExportVersion = 5;

    private List<Skill> Skills { get; set; } = [];
    private List<ModuleSlot> ModuleSlots { get; set; } = [];
    private List<PluginModule> PluginModules { get; set; } = [];
    private List<CraftingTable> CraftingTables { get; set; } = [];
    private List<Recipe> Recipes { get; set; } = [];
    private List<ItemOrTag> ItemOrTags { get; set; } = [];

    private void SetTrackedCollectionsFromServer(Server serverWithData)
    {
        Skills = serverWithData.Skills;
        ModuleSlots = serverWithData.ModuleSlots;
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
            options.Converters.Add(new JsonStringEnumConverter());

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

            if (!SupportedVersions.Contains(importedData.Version)) throw new ImportException(localizationService.GetTranslation("ServerManagement.Snackbar.UploadWrongVersion", string.Join(", ", SupportedVersions)));

            ImportSkills(context, serverWithData, importedData.Skills);
            ImportModuleSlots(context, serverWithData, importedData.ModuleSlots);
            errorCount += ImportItems(context, serverWithData, importedData.Items, out itemErrorNames);
            ImportTags(context, serverWithData, importedData.Tags);
            errorCount += ImportRecipes(context, serverWithData, importedData.Recipes, out recipeErrorNames);
            ApplyBuildingData(serverWithData, importedData);
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
            ImportModuleSlots(context, targetServerWithData, data.ModuleSlots);
            ImportItems(context, targetServerWithData, data.Items, out _);
            ImportTags(context, targetServerWithData, data.Tags);
            ImportRecipes(context, targetServerWithData, data.Recipes, out _);
            ApplyBuildingData(targetServerWithData, data);

            targetServerWithData.LastDataUploadTime = DateTimeOffset.UtcNow;
        });
    }

    // Les blocs Building/HousingConfig sont conservés bruts (jsonb) : le moteur du planificateur les relit
    // avec son propre modèle, et ils ne participent à aucune requête.
    private static void ApplyBuildingData(Server server, ImportDataDto importedData)
    {
        var hasBuildingData = importedData.Version >= 5 && importedData.Building is not null && importedData.HousingConfig is not null;

        server.HasBuildingData = hasBuildingData;
        server.BuildingConfigJson = hasBuildingData ? importedData.Building!.Value.GetRawText() : null;
        server.HousingConfigJson = hasBuildingData ? importedData.HousingConfig!.Value.GetRawText() : null;
    }
}
