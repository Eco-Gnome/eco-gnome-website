using ecocraft.Models;

namespace ecocraft.Services;

public enum EconomyGroupBy
{
    Item,
    Recipe,
    Skill,
    Player
}

public enum EconomyGlobalSortBy
{
    Label,
    PriceMin,
    PriceAverage,
    PriceMax,
    MarginMin,
    MarginAverage,
    MarginMax,
    ConfiguredPlayers,
    ConfiguredContexts,
    Spread
}

public enum EconomyComparisonEntity
{
    Item,
    Recipe,
    Skill
}

public enum EconomyComparisonSortBy
{
    Entity,
    Player1Value,
    Player2Value,
    DeltaAbs,
    DeltaPct
}

public class EconomyGlobalQuery
{
    public Guid ServerId { get; set; }
    public EconomyGroupBy GroupBy { get; set; } = EconomyGroupBy.Item;
    public string? SearchText { get; set; }
    public Guid? ItemOrTagId { get; set; }
    public Guid? SkillId { get; set; }
    public Guid? UserServerId { get; set; }
    public EconomyGlobalSortBy SortBy { get; set; } = EconomyGlobalSortBy.Label;
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class EconomyGlobalPlayerDetailRow
{
    public Guid UserServerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public Guid? DataContextId { get; set; }
    public string? DataContextName { get; set; }
    public bool IsDefaultContext { get; set; }
    public Guid? PrimaryRecipeId { get; set; }
    public string? PrimaryRecipeName { get; set; }
    public decimal? ConfiguredPrice { get; set; }
    public decimal? ConfiguredMargin { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceAverage { get; set; }
    public decimal? PriceMax { get; set; }
    public decimal? MarginMin { get; set; }
    public decimal? MarginAverage { get; set; }
    public decimal? MarginMax { get; set; }
    public int ConfiguredContextsCount { get; set; }
    public decimal? Spread { get; set; }
}

public class EconomyGlobalRow
{
    public string GroupKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? SecondaryLabel { get; set; }
    public bool? IsTag { get; set; }
    public Guid? ItemOrTagId { get; set; }
    public Guid? RecipeId { get; set; }
    public Guid? SkillId { get; set; }
    public Guid? UserServerId { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceAverage { get; set; }
    public decimal? PriceMax { get; set; }
    public decimal? MarginMin { get; set; }
    public decimal? MarginAverage { get; set; }
    public decimal? MarginMax { get; set; }
    public int ConfiguredPlayersCount { get; set; }
    public int ConfiguredContextsCount { get; set; }
    public decimal? Spread { get; set; }
    public List<EconomyGlobalPlayerDetailRow> PlayerDetails { get; set; } = [];
}

public class EconomyGlobalResult
{
    public List<EconomyGlobalRow> Rows { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class EconomyPlayerDetailQuery
{
    public Guid ServerId { get; set; }
    public Guid RequestingUserServerId { get; set; }
    public Guid Player1UserServerId { get; set; }
    public Guid Player2UserServerId { get; set; }
    public Guid? Player1DataContextId { get; set; }
    public Guid? Player2DataContextId { get; set; }
    public EconomyComparisonEntity ComparisonBy { get; set; } = EconomyComparisonEntity.Item;
    public EconomyComparisonSortBy SortBy { get; set; } = EconomyComparisonSortBy.DeltaAbs;
    public bool SortDescending { get; set; } = true;
    public string? SearchText { get; set; }
    public bool OnlyDifferences { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

public class EconomyPlayerContextOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class EconomyPlayerContextSummary
{
    public Guid DataContextId { get; set; }
    public string DataContextName { get; set; } = string.Empty;
    public int ConfiguredItemCount { get; set; }
    public decimal? AveragePrice { get; set; }
    public decimal? AverageMargin { get; set; }
    public int RecipeCount { get; set; }
    public int SkillCount { get; set; }
    public bool? ApplyMarginBetweenSkills { get; set; }
    public MarginType? MarginType { get; set; }
    public decimal? CalorieCost { get; set; }
}

public class EconomyPlayerCraftingTableSummary
{
    public Guid DataContextId { get; set; }
    public Guid CraftingTableId { get; set; }
    public string CraftingTableName { get; set; } = string.Empty;
    public Guid? PluginModuleId { get; set; }
    public string? PluginModuleName { get; set; }
    public List<Guid> SkilledPluginModuleIds { get; set; } = [];
    public decimal CraftMinuteFee { get; set; }
}

public class EconomyPlayerComparisonRow
{
    public string EntityKey { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? SecondaryLabel { get; set; }
    public Guid? ItemOrTagId { get; set; }
    public Guid? RecipeId { get; set; }
    public Guid? SkillId { get; set; }
    public bool? IsTag { get; set; }
    public decimal? Player1Value { get; set; }
    public decimal? Player2Value { get; set; }
    public decimal? DeltaAbs { get; set; }
    public decimal? DeltaPct { get; set; }
    public decimal? Player1Margin { get; set; }
    public decimal? Player2Margin { get; set; }
    public decimal? MarginDeltaAbs { get; set; }
    public string Status { get; set; } = "NoData";
    public bool IsDifferent { get; set; }
}

public class EconomyPlayerDetailResult
{
    public Guid Player1UserServerId { get; set; }
    public Guid Player2UserServerId { get; set; }
    public string Player1Name { get; set; } = string.Empty;
    public string Player2Name { get; set; } = string.Empty;
    public List<EconomyPlayerContextOption> Player1Contexts { get; set; } = [];
    public List<EconomyPlayerContextOption> Player2Contexts { get; set; } = [];
    public Guid? SelectedPlayer1DataContextId { get; set; }
    public Guid? SelectedPlayer2DataContextId { get; set; }
    public List<EconomyPlayerContextSummary> Player1ContextSummaries { get; set; } = [];
    public List<EconomyPlayerContextSummary> Player2ContextSummaries { get; set; } = [];
    public List<EconomyPlayerCraftingTableSummary> Player1CraftingTableSummaries { get; set; } = [];
    public List<EconomyPlayerCraftingTableSummary> Player2CraftingTableSummaries { get; set; } = [];
    public List<EconomyPlayerComparisonRow> ComparisonRows { get; set; } = [];
    public int TotalComparisonCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
