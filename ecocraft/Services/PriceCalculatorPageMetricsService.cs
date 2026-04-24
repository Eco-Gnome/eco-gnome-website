using Microsoft.Extensions.Logging;

namespace ecocraft.Services;

public class PriceCalculatorPageMetricsService(
    AppSettingsService appSettingsService,
    ILogger<PriceCalculatorPageMetricsService> logger)
{
    public Task<bool> IsEnabledAsync()
    {
        return appSettingsService.IsPriceCalculatorMetricsEnabledAsync();
    }

    public async Task<PriceCalculatorRefreshMetricsScope> CreateRefreshScopeAsync(long requestId)
    {
        var isEnabled = await appSettingsService.IsPriceCalculatorMetricsEnabledAsync();
        return new PriceCalculatorRefreshMetricsScope(requestId, isEnabled);
    }

    public void LogRefreshRenderCompleted(long requestId, long totalRefreshToRenderMilliseconds)
    {
        logger.LogInformation(
            "PriceCalculator refresh end-to-end render completed. requestId={RequestId} totalRefreshToRenderMs={TotalRefreshToRenderMs}",
            requestId,
            totalRefreshToRenderMilliseconds);
    }

    public void LogRefreshMetrics(PriceCalculatorRefreshMetrics metrics)
    {
        logger.LogInformation(
            "PriceCalculator refresh metrics. requestId={RequestId} totalMs={TotalMs} selectDataContextMs={SelectDataContextMs} localStorageMs={LocalStorageMs} serverDataLoadMs={ServerDataLoadMs} policySyncMs={PolicySyncMs} dataContextLoadMs={DataContextLoadMs} policyEnforcementMs={PolicyEnforcementMs} calculateRequestMs={CalculateRequestMs} skills={UserSkills} recipes={UserRecipes} elements={UserElements} prices={UserPrices} itemOrTags={ItemOrTags} serverRecipes={ServerRecipes} serverDataSource={ServerDataSource}",
            metrics.RequestId,
            metrics.TotalMilliseconds,
            metrics.SelectDataContextMilliseconds,
            metrics.LocalStorageMilliseconds,
            metrics.ServerDataLoadMilliseconds,
            metrics.PolicySyncMilliseconds,
            metrics.DataContextLoadMilliseconds,
            metrics.PolicyEnforcementMilliseconds,
            metrics.CalculateRequestMilliseconds,
            metrics.UserSkills,
            metrics.UserRecipes,
            metrics.UserElements,
            metrics.UserPrices,
            metrics.ItemOrTags,
            metrics.ServerRecipes,
            metrics.ServerDataSource);

        logger.LogInformation(
            "PriceCalculator refresh serverDataLoad details. requestId={RequestId} totalMs={TotalMs} dbFetchMs={DbFetchMs} graphMetricsMs={GraphMetricsMs} serverDataSource={ServerDataSource} skills={Skills} talents={Talents} craftingTables={CraftingTables} craftingTablePluginModules={CraftingTablePluginModules} pluginModules={PluginModules} recipes={Recipes} recipeElements={RecipeElements} itemOrTags={ItemOrTags} associatedItems={AssociatedItems} associatedTags={AssociatedTags}",
            metrics.RequestId,
            metrics.ServerDataLoadMilliseconds,
            metrics.ServerDataLoadDbFetchMilliseconds,
            metrics.ServerDataLoadGraphMetricsMilliseconds,
            metrics.ServerDataSource,
            metrics.ServerDataGraph.Skills,
            metrics.ServerDataGraph.Talents,
            metrics.ServerDataGraph.CraftingTables,
            metrics.ServerDataGraph.CraftingTablePluginModules,
            metrics.ServerDataGraph.PluginModules,
            metrics.ServerDataGraph.Recipes,
            metrics.ServerDataGraph.RecipeElements,
            metrics.ServerDataGraph.ItemOrTags,
            metrics.ServerDataGraph.AssociatedItems,
            metrics.ServerDataGraph.AssociatedTags);

        logger.LogInformation(
            "PriceCalculator refresh dataContextLoad details. requestId={RequestId} totalMs={TotalMs} graphMetricsMs={GraphMetricsMs} userSkills={UserSkills} userTalents={UserTalents} userCraftingTables={UserCraftingTables} userCraftingTableSkilledPluginModules={UserCraftingTableSkilledPluginModules} userRecipes={UserRecipes} userElements={UserElements} userPrices={UserPrices} userMargins={UserMargins} userSettings={UserSettings}",
            metrics.RequestId,
            metrics.DataContextLoadMilliseconds,
            metrics.DataContextGraphMetricsMilliseconds,
            metrics.DataContextGraph.UserSkills,
            metrics.DataContextGraph.UserTalents,
            metrics.DataContextGraph.UserCraftingTables,
            metrics.DataContextGraph.UserCraftingTableSkilledPluginModules,
            metrics.DataContextGraph.UserRecipes,
            metrics.DataContextGraph.UserElements,
            metrics.DataContextGraph.UserPrices,
            metrics.DataContextGraph.UserMargins,
            metrics.DataContextGraph.UserSettings);

        logger.LogInformation(
            "PriceCalculator refresh policyEnforcement details. requestId={RequestId} totalMs={TotalMs} caloriePolicyMs={CaloriePolicyMs} marginPolicyMs={MarginPolicyMs}",
            metrics.RequestId,
            metrics.PolicyEnforcementMilliseconds,
            metrics.CaloriePolicyMilliseconds,
            metrics.MarginPolicyMilliseconds);
    }

    public void LogCalculationPipelineMetrics(PriceCalculatorCalculationPipelineMetrics metrics)
    {
        logger.LogInformation(
            "PriceCalculator calculation pipeline metrics. trigger={TriggerOrigin} caller={Caller} totalMs={TotalMs} serviceCalculateMs={ServiceCalculateMs} displayCacheRefreshMs={DisplayCacheRefreshMs} itemsToBuy={ItemsToBuy} itemsToSell={ItemsToSell} autoCalculatedSellIds={AutoCalculatedSellIds} userElementsByElementId={UserElementsByElementId}",
            metrics.TriggerOrigin,
            metrics.Caller,
            metrics.TotalMilliseconds,
            metrics.ServiceCalculateMilliseconds,
            metrics.DisplayCacheRefreshMilliseconds,
            metrics.DisplayCacheMetrics.ItemsToBuyCount,
            metrics.DisplayCacheMetrics.ItemsToSellCount,
            metrics.DisplayCacheMetrics.AutoCalculatedSellCount,
            metrics.DisplayCacheMetrics.UserElementsByElementIdCount);

        logger.LogInformation(
            "PriceCalculator display cache refresh details. trigger={TriggerOrigin} caller={Caller} totalMs={TotalMs} categorizeForDisplayMs={CategorizeForDisplayMs} categorizeForAutoSellMs={CategorizeForAutoSellMs} autoSellIdProjectionMs={AutoSellIdProjectionMs} userElementsIndexMs={UserElementsIndexMs}",
            metrics.TriggerOrigin,
            metrics.Caller,
            metrics.DisplayCacheMetrics.TotalMilliseconds,
            metrics.DisplayCacheMetrics.CategorizeForDisplayMilliseconds,
            metrics.DisplayCacheMetrics.CategorizeForAutoSellMilliseconds,
            metrics.DisplayCacheMetrics.AutoSellIdProjectionMilliseconds,
            metrics.DisplayCacheMetrics.UserElementsIndexMilliseconds);
    }
}
