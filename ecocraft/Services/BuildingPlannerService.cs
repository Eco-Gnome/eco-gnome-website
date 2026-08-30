using ecocraft.BuildingPlanner;
using ecocraft.BuildingPlanner.Model;
using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

// Façade du planificateur pour les composants : disponibilité, analyse, plans sauvegardés, liens de partage.
public sealed class BuildingPlannerService(
    IDbContextFactory<EcoCraftDbContext> factory,
    BuildingPlanDbService buildingPlanDbService,
    BuildingPlannerCatalogService catalogService,
    ContextService contextService)
{
    public bool HasBuildingData => contextService.CurrentServer?.HasBuildingData ?? false;

    public Catalog GetCatalog(Server serverData, DataContext? dataContext) => catalogService.GetCatalog(serverData, dataContext);

    public ClientCatalog GetClientCatalog(Catalog catalog, Server serverData) => catalogService.BuildClientCatalog(catalog, serverData);

    public AnalysisResult Analyze(PlanDocument document, Catalog catalog) => PlanAnalyzer.Analyze(document, catalog);

    public Task<List<BuildingPlan>> ListPlansAsync()
    {
        var userServer = contextService.CurrentUserServer;
        return userServer is null ? Task.FromResult(new List<BuildingPlan>()) : buildingPlanDbService.GetSummariesByUserServerAsync(userServer);
    }

    // Lecture par identifiant sans contrôle de propriétaire : c'est le mécanisme de partage « ?id= ».
    public Task<BuildingPlan?> LoadPlanAsync(Guid id) => buildingPlanDbService.GetByIdAsync(id);

    public bool OwnsPlan(BuildingPlan plan) => contextService.CurrentUserServer?.Id == plan.UserServerId;

    public async Task<BuildingPlan> SavePlanAsync(BuildingPlan? existing, string name, PlanDocument document)
    {
        var userServer = contextService.CurrentUserServer ?? throw new InvalidOperationException("No current user server.");
        var now = DateTimeOffset.UtcNow;
        document.Name = name;
        var json = PlanDocumentJson.Serialize(document);

        if (existing is not null && OwnsPlan(existing))
        {
            existing.Name = name;
            existing.SchemaVersion = document.SchemaVersion;
            existing.Document = json;
            existing.UpdateDateTime = now;
            await EcoCraftDbContext.ContextSaveAsync(factory, context =>
            {
                buildingPlanDbService.UpdateContent(context, existing);
                return Task.CompletedTask;
            });
            return existing;
        }

        var plan = new BuildingPlan
        {
            UserServerId = userServer.Id,
            Name = name,
            SchemaVersion = document.SchemaVersion,
            Document = json,
            CreationDateTime = now,
            UpdateDateTime = now,
        };
        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            buildingPlanDbService.Create(context, plan);
            return Task.CompletedTask;
        });
        return plan;
    }

    public async Task DeletePlanAsync(BuildingPlan plan)
    {
        if (!OwnsPlan(plan)) return;
        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            buildingPlanDbService.Destroy(context, plan);
            return Task.CompletedTask;
        });
    }

    // Lien de partage : le plan compressé dans l'URL tant qu'il reste court, sinon l'identifiant du plan sauvegardé.
    public (string? Url, bool NeedsSave) BuildShareUrl(string baseUri, PlanDocument document, Guid? savedPlanId)
    {
        var encoded = PlanUrlCodec.Encode(document);
        var root = baseUri.TrimEnd('/') + "/building-planner";
        if (encoded.Length <= PlanUrlCodec.MaxUrlPayloadLength) return ($"{root}?plan={encoded}", false);
        return savedPlanId is { } id ? ($"{root}?id={id}", false) : (null, true);
    }
}
