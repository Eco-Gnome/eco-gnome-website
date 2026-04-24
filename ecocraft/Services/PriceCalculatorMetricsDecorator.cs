using System.Diagnostics;
using ecocraft.Models;
using Microsoft.Extensions.Logging;

namespace ecocraft.Services;

public class PriceCalculatorMetricsDecorator(
    PriceCalculatorService inner,
    AppSettingsService appSettingsService,
    ILogger<PriceCalculatorMetricsDecorator> logger) : IPriceCalculatorService
{
    public (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTags(DataContext dataContext)
    {
        return inner.GetCategorizedItemOrTags(dataContext);
    }

    public (List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) GetCategorizedItemOrTagsForDisplay(DataContext dataContext)
    {
        return inner.GetCategorizedItemOrTagsForDisplay(dataContext);
    }

    public async Task<PriceCalculationMetrics> Calculate(DataContext dataContext, string triggerOrigin = "Unknown", bool debug = false)
    {
        var isMetricsEnabled = await appSettingsService.IsPriceCalculatorMetricsEnabledAsync();
        if (!isMetricsEnabled)
        {
            return await inner.Calculate(dataContext, triggerOrigin, debug);
        }

        var totalStopwatch = Stopwatch.StartNew();
        var metrics = await inner.Calculate(dataContext, triggerOrigin, debug);
        totalStopwatch.Stop();

        logger.LogInformation(
            "Price calculation metrics. trigger={TriggerOrigin} totalMs={TotalMs} iterations={Iterations} handledRecipes={HandledRecipes} deferredIngredients={DeferredIngredients} deferredReintegrated={DeferredReintegrated} dirtyUserPrices={DirtyUserPrices} dirtyUserElements={DirtyUserElements} dbUserPriceUpdates={DbUserPriceUpdates} dbUserElementUpdates={DbUserElementUpdates}",
            metrics.TriggerOrigin,
            totalStopwatch.ElapsedMilliseconds,
            metrics.IterationCount,
            metrics.HandledRecipes,
            metrics.DeferredIngredientRecipes,
            metrics.DeferredReintegratedRecipes,
            metrics.DirtyUserPrices,
            metrics.DirtyUserElements,
            metrics.DbUserPriceUpdates,
            metrics.DbUserElementUpdates);

        return metrics with { TotalMilliseconds = totalStopwatch.ElapsedMilliseconds };
    }
}
