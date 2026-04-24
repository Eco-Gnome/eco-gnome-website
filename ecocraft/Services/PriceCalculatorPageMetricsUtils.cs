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

public static class PriceCalculatorMetricsGraphUtils
{
    public static PriceCalculatorServerDataGraphMetrics BuildServerDataGraphMetrics(Server? server)
    {
        if (server is null)
        {
            return PriceCalculatorServerDataGraphMetrics.Empty;
        }

        return new PriceCalculatorServerDataGraphMetrics
        {
            Skills = server.Skills.Count,
            Talents = server.Skills.Sum(skill => skill.Talents.Count),
            CraftingTables = server.CraftingTables.Count,
            CraftingTablePluginModules = server.CraftingTables.Sum(table => table.PluginModules.Count),
            PluginModules = server.PluginModules.Count,
            Recipes = server.Recipes.Count,
            RecipeElements = server.Recipes.Sum(recipe => recipe.Elements.Count),
            ItemOrTags = server.ItemOrTags.Count,
            AssociatedItems = server.ItemOrTags.Sum(itemOrTag => itemOrTag.AssociatedItems.Count),
            AssociatedTags = server.ItemOrTags.Sum(itemOrTag => itemOrTag.AssociatedTags.Count),
        };
    }

    public static PriceCalculatorDataContextGraphMetrics BuildDataContextGraphMetrics(DataContext? dataContext)
    {
        if (dataContext is null)
        {
            return PriceCalculatorDataContextGraphMetrics.Empty;
        }

        return new PriceCalculatorDataContextGraphMetrics
        {
            UserSkills = dataContext.UserSkills.Count,
            UserTalents = dataContext.UserTalents.Count,
            UserCraftingTables = dataContext.UserCraftingTables.Count,
            UserCraftingTableSkilledPluginModules = dataContext.UserCraftingTables.Sum(table => table.SkilledPluginModules.Count),
            UserRecipes = dataContext.UserRecipes.Count,
            UserElements = dataContext.UserElements.Count,
            UserPrices = dataContext.UserPrices.Count,
            UserMargins = dataContext.UserMargins.Count,
            UserSettings = dataContext.UserSettings.Count,
        };
    }
}

public static class PriceCalculatorMetricsStopwatchUtils
{
    public static Stopwatch? Create(bool enabled, bool startNow = false)
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

public class PriceCalculatorRefreshMetricsScope
{
    public PriceCalculatorRefreshMetricsScope(long requestId, bool isEnabled)
    {
        RequestId = requestId;
        IsEnabled = isEnabled;
        TotalStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled, startNow: true);
        SelectDataContextStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        LocalStorageStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        ServerDataLoadStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        ServerDataLoadDbFetchStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        ServerDataLoadGraphMetricsStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        PolicySyncStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        DataContextLoadStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        DataContextGraphMetricsStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        PolicyEnforcementStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        CaloriePolicyStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        MarginPolicyStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        CalculateRequestStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
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
}

public class PriceCalculatorCalculationMetricsScope
{
    public PriceCalculatorCalculationMetricsScope(string triggerOrigin, string caller, bool isEnabled)
    {
        TriggerOrigin = triggerOrigin;
        Caller = caller;
        IsEnabled = isEnabled;
        TotalStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled, startNow: true);
        ServiceCalculateStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        DisplayCacheRefreshStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
    }

    public string TriggerOrigin { get; }
    public string Caller { get; }
    public bool IsEnabled { get; }

    public Stopwatch? TotalStopwatch { get; }
    public Stopwatch? ServiceCalculateStopwatch { get; }
    public Stopwatch? DisplayCacheRefreshStopwatch { get; }
    public PriceCalculatorDisplayCacheRefreshMetrics DisplayCacheMetrics { get; set; } = PriceCalculatorDisplayCacheRefreshMetrics.Empty;

    public PriceCalculatorCalculationPipelineMetrics ToCalculationPipelineMetrics()
    {
        return new PriceCalculatorCalculationPipelineMetrics(
            TriggerOrigin,
            Caller,
            TotalStopwatch?.ElapsedMilliseconds ?? 0,
            ServiceCalculateStopwatch?.ElapsedMilliseconds ?? 0,
            DisplayCacheRefreshStopwatch?.ElapsedMilliseconds ?? 0,
            DisplayCacheMetrics);
    }
}

public class PriceCalculatorDisplayCacheMetricsScope
{
    public PriceCalculatorDisplayCacheMetricsScope(bool isEnabled)
    {
        TotalStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled, startNow: true);
        CategorizeForDisplayStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        CategorizeForAutoSellStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        AutoSellIdProjectionStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
        UserElementsIndexStopwatch = PriceCalculatorMetricsStopwatchUtils.Create(isEnabled);
    }

    public Stopwatch? TotalStopwatch { get; }
    public Stopwatch? CategorizeForDisplayStopwatch { get; }
    public Stopwatch? CategorizeForAutoSellStopwatch { get; }
    public Stopwatch? AutoSellIdProjectionStopwatch { get; }
    public Stopwatch? UserElementsIndexStopwatch { get; }

    public PriceCalculatorDisplayCacheRefreshMetrics ToMetrics(
        int itemsToBuyCount,
        int itemsToSellCount,
        int autoCalculatedSellCount,
        int userElementsByElementIdCount)
    {
        return new PriceCalculatorDisplayCacheRefreshMetrics
        {
            TotalMilliseconds = TotalStopwatch?.ElapsedMilliseconds ?? 0,
            CategorizeForDisplayMilliseconds = CategorizeForDisplayStopwatch?.ElapsedMilliseconds ?? 0,
            CategorizeForAutoSellMilliseconds = CategorizeForAutoSellStopwatch?.ElapsedMilliseconds ?? 0,
            AutoSellIdProjectionMilliseconds = AutoSellIdProjectionStopwatch?.ElapsedMilliseconds ?? 0,
            UserElementsIndexMilliseconds = UserElementsIndexStopwatch?.ElapsedMilliseconds ?? 0,
            ItemsToBuyCount = itemsToBuyCount,
            ItemsToSellCount = itemsToSellCount,
            AutoCalculatedSellCount = autoCalculatedSellCount,
            UserElementsByElementIdCount = userElementsByElementIdCount
        };
    }
}
