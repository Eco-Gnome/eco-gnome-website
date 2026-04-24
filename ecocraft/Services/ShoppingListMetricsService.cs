using Microsoft.Extensions.Logging;

namespace ecocraft.Services;

public class ShoppingListMetricsScope(long requestId, bool isEnabled)
{
    public long RequestId { get; } = requestId;
    public bool IsEnabled { get; } = isEnabled;
}

public class ShoppingListMetricsService(
    AppSettingsService appSettingsService,
    ILogger<ShoppingListMetricsService> logger)
{
    public async Task<ShoppingListMetricsScope> CreateScopeAsync(long requestId)
    {
        var isEnabled = await appSettingsService.IsPriceCalculatorMetricsEnabledAsync();
        return new ShoppingListMetricsScope(requestId, isEnabled);
    }

    public Task LogRetrieveDataAsync(ShoppingListMetricsScope scope, string serverDataSource, bool forceServerReload)
    {
        if (!scope.IsEnabled)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "ShoppingList retrieve data. requestId={RequestId} serverDataSource={ServerDataSource} forceServerReload={ForceServerReload}",
            scope.RequestId,
            serverDataSource,
            forceServerReload);

        return Task.CompletedTask;
    }

    public Task LogReferenceDataContextLoadFailedAsync(ShoppingListMetricsScope scope, Guid? referenceDataContextId, Exception exception)
    {
        if (!scope.IsEnabled)
        {
            return Task.CompletedTask;
        }

        logger.LogWarning(
            exception,
            "ShoppingList failed to load reference data context. requestId={RequestId} referenceDataContextId={ReferenceDataContextId}",
            scope.RequestId,
            referenceDataContextId);

        return Task.CompletedTask;
    }

    public Task LogShoppingListLoadFailedAsync(ShoppingListMetricsScope scope, Guid targetShoppingListId, Exception exception)
    {
        if (!scope.IsEnabled)
        {
            return Task.CompletedTask;
        }

        logger.LogWarning(
            exception,
            "ShoppingList failed to load selected list. requestId={RequestId} targetShoppingListId={TargetShoppingListId}",
            scope.RequestId,
            targetShoppingListId);

        return Task.CompletedTask;
    }

    public Task LogFallbackShoppingListLoadFailedAsync(ShoppingListMetricsScope scope, Guid fallbackShoppingListId, Exception exception)
    {
        if (!scope.IsEnabled)
        {
            return Task.CompletedTask;
        }

        logger.LogWarning(
            exception,
            "ShoppingList failed to load fallback list. requestId={RequestId} fallbackShoppingListId={FallbackShoppingListId}",
            scope.RequestId,
            fallbackShoppingListId);

        return Task.CompletedTask;
    }
}
