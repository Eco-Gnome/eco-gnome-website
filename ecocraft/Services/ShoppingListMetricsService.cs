using Microsoft.Extensions.Logging;

namespace ecocraft.Services;

public class ShoppingListMetricsService(
    AppSettingsService appSettingsService,
    ILogger<ShoppingListMetricsService> logger)
{
    public async Task LogRetrieveDataAsync(long requestId, string serverDataSource, bool forceServerReload)
    {
        if (!await appSettingsService.IsPriceCalculatorMetricsEnabledAsync())
        {
            return;
        }

        logger.LogInformation(
            "ShoppingList retrieve data. requestId={RequestId} serverDataSource={ServerDataSource} forceServerReload={ForceServerReload}",
            requestId,
            serverDataSource,
            forceServerReload);
    }

    public async Task LogReferenceDataContextLoadFailedAsync(long requestId, Guid? referenceDataContextId, Exception exception)
    {
        if (!await appSettingsService.IsPriceCalculatorMetricsEnabledAsync())
        {
            return;
        }

        logger.LogWarning(
            exception,
            "ShoppingList failed to load reference data context. requestId={RequestId} referenceDataContextId={ReferenceDataContextId}",
            requestId,
            referenceDataContextId);
    }

    public async Task LogShoppingListLoadFailedAsync(long requestId, Guid targetShoppingListId, Exception exception)
    {
        if (!await appSettingsService.IsPriceCalculatorMetricsEnabledAsync())
        {
            return;
        }

        logger.LogWarning(
            exception,
            "ShoppingList failed to load selected list. requestId={RequestId} targetShoppingListId={TargetShoppingListId}",
            requestId,
            targetShoppingListId);
    }

    public async Task LogFallbackShoppingListLoadFailedAsync(long requestId, Guid fallbackShoppingListId, Exception exception)
    {
        if (!await appSettingsService.IsPriceCalculatorMetricsEnabledAsync())
        {
            return;
        }

        logger.LogWarning(
            exception,
            "ShoppingList failed to load fallback list. requestId={RequestId} fallbackShoppingListId={FallbackShoppingListId}",
            requestId,
            fallbackShoppingListId);
    }
}
