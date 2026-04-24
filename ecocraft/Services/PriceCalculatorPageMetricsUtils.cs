using System.Diagnostics;
using ecocraft.Models;

namespace ecocraft.Services;

public class PriceCalculatorServerDataGraphMetrics
{
    public static PriceCalculatorServerDataGraphMetrics Empty { get; } = new();

    public int Skills { get; init; }
    public int Talents { get; init; }
    public int CraftingTables { get; init; }
    public int CraftingTablePluginModules { get; init; }
    public int PluginModules { get; init; }
    public int Recipes { get; init; }
    public int RecipeElements { get; init; }
    public int ItemOrTags { get; init; }
    public int AssociatedItems { get; init; }
    public int AssociatedTags { get; init; }
}

public class PriceCalculatorDataContextGraphMetrics
{
    public static PriceCalculatorDataContextGraphMetrics Empty { get; } = new();

    public int UserSkills { get; init; }
    public int UserTalents { get; init; }
    public int UserCraftingTables { get; init; }
    public int UserCraftingTableSkilledPluginModules { get; init; }
    public int UserRecipes { get; init; }
    public int UserElements { get; init; }
    public int UserPrices { get; init; }
    public int UserMargins { get; init; }
    public int UserSettings { get; init; }
}

public class PriceCalculatorDisplayCacheRefreshMetrics
{
    public static PriceCalculatorDisplayCacheRefreshMetrics Empty { get; } = new();

    public long TotalMilliseconds { get; init; }
    public long CategorizeForDisplayMilliseconds { get; init; }
    public long CategorizeForAutoSellMilliseconds { get; init; }
    public long AutoSellIdProjectionMilliseconds { get; init; }
    public long UserElementsIndexMilliseconds { get; init; }
    public int ItemsToBuyCount { get; init; }
    public int ItemsToSellCount { get; init; }
    public int AutoCalculatedSellCount { get; init; }
    public int UserElementsByElementIdCount { get; init; }
}

public record PriceCalculatorRefreshMetrics(
    long RequestId,
    long TotalMilliseconds,
    long SelectDataContextMilliseconds,
    long LocalStorageMilliseconds,
    long ServerDataLoadMilliseconds,
    long PolicySyncMilliseconds,
    long DataContextLoadMilliseconds,
    long PolicyEnforcementMilliseconds,
    long CalculateRequestMilliseconds,
    int UserSkills,
    int UserRecipes,
    int UserElements,
    int UserPrices,
    int ItemOrTags,
    int ServerRecipes,
    string ServerDataSource,
    long ServerDataLoadDbFetchMilliseconds,
    long ServerDataLoadGraphMetricsMilliseconds,
    PriceCalculatorServerDataGraphMetrics ServerDataGraph,
    long DataContextGraphMetricsMilliseconds,
    PriceCalculatorDataContextGraphMetrics DataContextGraph,
    long CaloriePolicyMilliseconds,
    long MarginPolicyMilliseconds);

public record PriceCalculatorCalculationPipelineMetrics(
    string TriggerOrigin,
    string Caller,
    long TotalMilliseconds,
    long ServiceCalculateMilliseconds,
    long DisplayCacheRefreshMilliseconds,
    PriceCalculatorDisplayCacheRefreshMetrics DisplayCacheMetrics);

public class PriceCalculatorRefreshMetricsScope
{
    public PriceCalculatorRefreshMetricsScope(long requestId, bool isEnabled)
    {
        RequestId = requestId;
        IsEnabled = isEnabled;
        TotalStopwatch = CreateStopwatch(isEnabled, startNow: true);
        SelectDataContextStopwatch = CreateStopwatch(isEnabled);
        LocalStorageStopwatch = CreateStopwatch(isEnabled);
        ServerDataLoadStopwatch = CreateStopwatch(isEnabled);
        ServerDataLoadDbFetchStopwatch = CreateStopwatch(isEnabled);
        ServerDataLoadGraphMetricsStopwatch = CreateStopwatch(isEnabled);
        PolicySyncStopwatch = CreateStopwatch(isEnabled);
        DataContextLoadStopwatch = CreateStopwatch(isEnabled);
        DataContextGraphMetricsStopwatch = CreateStopwatch(isEnabled);
        PolicyEnforcementStopwatch = CreateStopwatch(isEnabled);
        CaloriePolicyStopwatch = CreateStopwatch(isEnabled);
        MarginPolicyStopwatch = CreateStopwatch(isEnabled);
        CalculateRequestStopwatch = CreateStopwatch(isEnabled);
    }

    public long RequestId { get; }
    public bool IsEnabled { get; }

    public Stopwatch? TotalStopwatch { get; }
    public Stopwatch? SelectDataContextStopwatch { get; }
    public Stopwatch? LocalStorageStopwatch { get; }
    public Stopwatch? ServerDataLoadStopwatch { get; }
    public Stopwatch? ServerDataLoadDbFetchStopwatch { get; }
    public Stopwatch? ServerDataLoadGraphMetricsStopwatch { get; }
    public Stopwatch? PolicySyncStopwatch { get; }
    public Stopwatch? DataContextLoadStopwatch { get; }
    public Stopwatch? DataContextGraphMetricsStopwatch { get; }
    public Stopwatch? PolicyEnforcementStopwatch { get; }
    public Stopwatch? CaloriePolicyStopwatch { get; }
    public Stopwatch? MarginPolicyStopwatch { get; }
    public Stopwatch? CalculateRequestStopwatch { get; }

    public string ServerDataSource { get; set; } = "cache";
    public PriceCalculatorServerDataGraphMetrics ServerDataGraph { get; set; } = PriceCalculatorServerDataGraphMetrics.Empty;
    public PriceCalculatorDataContextGraphMetrics DataContextGraph { get; set; } = PriceCalculatorDataContextGraphMetrics.Empty;

    public PriceCalculatorRefreshMetrics ToRefreshMetrics(DataContext selectedDataContext, Server? currentServerData)
    {
        return new PriceCalculatorRefreshMetrics(
            RequestId,
            TotalStopwatch?.ElapsedMilliseconds ?? 0,
            SelectDataContextStopwatch?.ElapsedMilliseconds ?? 0,
            LocalStorageStopwatch?.ElapsedMilliseconds ?? 0,
            ServerDataLoadStopwatch?.ElapsedMilliseconds ?? 0,
            PolicySyncStopwatch?.ElapsedMilliseconds ?? 0,
            DataContextLoadStopwatch?.ElapsedMilliseconds ?? 0,
            PolicyEnforcementStopwatch?.ElapsedMilliseconds ?? 0,
            CalculateRequestStopwatch?.ElapsedMilliseconds ?? 0,
            selectedDataContext.UserSkills.Count,
            selectedDataContext.UserRecipes.Count,
            selectedDataContext.UserElements.Count,
            selectedDataContext.UserPrices.Count,
            currentServerData?.ItemOrTags.Count ?? 0,
            currentServerData?.Recipes.Count ?? 0,
            ServerDataSource,
            ServerDataLoadDbFetchStopwatch?.ElapsedMilliseconds ?? 0,
            ServerDataLoadGraphMetricsStopwatch?.ElapsedMilliseconds ?? 0,
            ServerDataGraph,
            DataContextGraphMetricsStopwatch?.ElapsedMilliseconds ?? 0,
            DataContextGraph,
            CaloriePolicyStopwatch?.ElapsedMilliseconds ?? 0,
            MarginPolicyStopwatch?.ElapsedMilliseconds ?? 0);
    }

    private static Stopwatch? CreateStopwatch(bool enabled, bool startNow = false)
    {
        if (!enabled)
        {
            return null;
        }

        if (startNow)
        {
            return Stopwatch.StartNew();
        }

        return new Stopwatch();
    }
}
