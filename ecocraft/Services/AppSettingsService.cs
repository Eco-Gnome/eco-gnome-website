using System.Threading;
using ecocraft.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ecocraft.Services;

public class AppSettingsService(
    IDbContextFactory<EcoCraftDbContext> factory,
    ILogger<AppSettingsService> logger)
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isLoaded;
    private bool _priceCalculatorMetricsEnabled = true;

    public async Task<bool> IsPriceCalculatorMetricsEnabledAsync()
    {
        if (_isLoaded)
        {
            return _priceCalculatorMetricsEnabled;
        }

        await _syncLock.WaitAsync();
        try
        {
            if (_isLoaded)
            {
                return _priceCalculatorMetricsEnabled;
            }

            await using var context = await factory.CreateDbContextAsync();
            var appSetting = await context.AppSettings
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (appSetting is null)
            {
                appSetting = new AppSetting();
                context.AppSettings.Add(appSetting);
                await context.SaveChangesAsync();
            }

            _priceCalculatorMetricsEnabled = appSetting.PriceCalculatorMetricsEnabled;
            _isLoaded = true;
            return _priceCalculatorMetricsEnabled;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to load app settings. Falling back to current in-memory value.");
            return _priceCalculatorMetricsEnabled;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> SetPriceCalculatorMetricsEnabledAsync(bool isEnabled)
    {
        await _syncLock.WaitAsync();
        try
        {
            await using var context = await factory.CreateDbContextAsync();
            var appSetting = await context.AppSettings
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (appSetting is null)
            {
                appSetting = new AppSetting();
                context.AppSettings.Add(appSetting);
            }

            if (appSetting.PriceCalculatorMetricsEnabled != isEnabled)
            {
                appSetting.PriceCalculatorMetricsEnabled = isEnabled;
            }

            await context.SaveChangesAsync();

            _priceCalculatorMetricsEnabled = appSetting.PriceCalculatorMetricsEnabled;
            _isLoaded = true;
            return _priceCalculatorMetricsEnabled;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to save app settings.");
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
