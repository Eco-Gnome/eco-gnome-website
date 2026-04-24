using Microsoft.Extensions.Logging;

namespace ecocraft.Services;

public class AppSettingsService(ILogger<AppSettingsService> logger)
{
    private readonly object _syncLock = new();
    private bool _priceCalculatorMetricsEnabled = false;

    // Runtime-only setting (server RAM): intentionally not persisted in database.
    public Task<bool> IsPriceCalculatorMetricsEnabledAsync()
    {
        lock (_syncLock)
        {
            return Task.FromResult(_priceCalculatorMetricsEnabled);
        }
    }

    public Task<bool> SetPriceCalculatorMetricsEnabledAsync(bool isEnabled)
    {
        lock (_syncLock)
        {
            if (_priceCalculatorMetricsEnabled != isEnabled)
            {
                _priceCalculatorMetricsEnabled = isEnabled;
                logger.LogInformation("Price calculator metrics toggled to {IsEnabled}", _priceCalculatorMetricsEnabled);
            }

            return Task.FromResult(_priceCalculatorMetricsEnabled);
        }
    }
}
