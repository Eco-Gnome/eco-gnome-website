using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

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

public sealed class EconomyGlobalQuery
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

public sealed class EconomyGlobalPlayerDetailRow
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

public sealed class EconomyGlobalRow
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

public sealed class EconomyGlobalResult
{
    public List<EconomyGlobalRow> Rows { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class EconomyPlayerDetailQuery
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

public sealed class EconomyPlayerContextOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class EconomyPlayerContextSummary
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

public sealed class EconomyPlayerCraftingTableSummary
{
    public Guid DataContextId { get; set; }
    public Guid CraftingTableId { get; set; }
    public string CraftingTableName { get; set; } = string.Empty;
    public Guid? PluginModuleId { get; set; }
    public string? PluginModuleName { get; set; }
    public List<Guid> SkilledPluginModuleIds { get; set; } = [];
    public decimal CraftMinuteFee { get; set; }
}

public sealed class EconomyPlayerComparisonRow
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

public sealed class EconomyPlayerDetailResult
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

public sealed class EconomyViewerService(IDbContextFactory<EcoCraftDbContext> factory)
{
    private sealed record PriceFact(
        Guid UserId,
        Guid UserServerId,
        string PlayerName,
        Guid DataContextId,
        string DataContextName,
        bool IsDefaultContext,
        Guid ItemOrTagId,
        string ItemName,
        bool IsTag,
        decimal? EffectivePrice,
        decimal? Margin,
        Guid? PrimaryRecipeId,
        string? PrimaryRecipeName
    );

    private sealed record ProducedItemRecipeFact(
        Guid DataContextId,
        Guid ItemOrTagId,
        Guid RecipeId,
        string RecipeName
    );

    private sealed record RecipeFact(
        Guid UserId,
        Guid UserServerId,
        string PlayerName,
        Guid DataContextId,
        string DataContextName,
        bool IsDefaultContext,
        Guid RecipeId,
        string RecipeName,
        Guid? SkillId,
        string? SkillName,
        Guid ItemOrTagId,
        decimal? UnitPrice,
        decimal? Margin
    );

    private sealed record ContextPriceFact(Guid DataContextId, decimal? EffectivePrice, decimal? Margin);

    private sealed record RecipeCountFact(Guid DataContextId, int RecipeCount);
    private sealed record SkillCountFact(Guid DataContextId, int SkillCount);
    private sealed record ContextUserSettingFact(Guid DataContextId, bool ApplyMarginBetweenSkills, MarginType MarginType, decimal CalorieCost);

    public async Task<EconomyGlobalResult> GetGlobalAsync(EconomyGlobalQuery query, Guid requestingUserServerId)
    {
        var applyPaging = query.PageSize > 0;
        var page = Math.Max(1, query.Page);
        var pageSize = applyPaging ? Math.Clamp(query.PageSize, 10, 200) : 0;

        await using var context = await factory.CreateDbContextAsync();

        await EnsureUserServerMembershipAsync(context, query.ServerId, requestingUserServerId);

        if (query.UserServerId is Guid filteredUserServerId)
        {
            await EnsureUserServerMembershipAsync(context, query.ServerId, filteredUserServerId);
        }

        var rows = query.GroupBy switch
        {
            EconomyGroupBy.Item => await BuildGlobalRowsByItemAsync(context, query),
            EconomyGroupBy.Player => await BuildGlobalRowsByPlayerAsync(context, query),
            EconomyGroupBy.Recipe => BuildGlobalRowsByRecipe(await GetRecipeFactsAsync(context, query.ServerId), query),
            EconomyGroupBy.Skill => BuildGlobalRowsBySkill(await GetRecipeFactsAsync(context, query.ServerId), query),
            _ => []
        };

        rows = SortGlobalRows(rows, query.SortBy, query.SortDescending);

        var totalCount = rows.Count();
        var pageRows = applyPaging
            ? rows.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            : rows.ToList();

        return new EconomyGlobalResult
        {
            Rows = pageRows,
            TotalCount = totalCount,
            Page = applyPaging ? page : 1,
            PageSize = applyPaging ? pageSize : totalCount
        };
    }

    public async Task<EconomyPlayerDetailResult> GetPlayerDetailAsync(EconomyPlayerDetailQuery query)
    {
        var applyPaging = query.PageSize > 0;
        var page = Math.Max(1, query.Page);
        var pageSize = applyPaging ? Math.Clamp(query.PageSize, 10, 300) : 0;

        if (query.Player1UserServerId == query.Player2UserServerId)
        {
            throw new ArgumentException("Player1 and Player2 must be different users.");
        }

        await using var context = await factory.CreateDbContextAsync();

        await EnsureUserServerMembershipAsync(context, query.ServerId, query.RequestingUserServerId);
        await EnsureUserServerMembershipAsync(context, query.ServerId, query.Player1UserServerId);
        await EnsureUserServerMembershipAsync(context, query.ServerId, query.Player2UserServerId);

        var player1User = await context.UserServers
            .AsNoTracking()
            .Where(us => us.Id == query.Player1UserServerId)
            .Select(us => new
            {
                us.Id,
                PlayerName = us.Pseudo ?? us.User.Pseudo
            })
            .FirstAsync();

        var player2User = await context.UserServers
            .AsNoTracking()
            .Where(us => us.Id == query.Player2UserServerId)
            .Select(us => new
            {
                us.Id,
                PlayerName = us.Pseudo ?? us.User.Pseudo
            })
            .FirstAsync();

        var player1Contexts = await context.DataContexts
            .AsNoTracking()
            .Where(dc => dc.UserServerId == query.Player1UserServerId && !dc.IsShoppingList)
            .OrderByDescending(dc => dc.IsDefault)
            .ThenBy(dc => dc.Name)
            .Select(dc => new EconomyPlayerContextOption
            {
                Id = dc.Id,
                Name = dc.Name,
                IsDefault = dc.IsDefault
            })
            .ToListAsync();

        var player2Contexts = await context.DataContexts
            .AsNoTracking()
            .Where(dc => dc.UserServerId == query.Player2UserServerId && !dc.IsShoppingList)
            .OrderByDescending(dc => dc.IsDefault)
            .ThenBy(dc => dc.Name)
            .Select(dc => new EconomyPlayerContextOption
            {
                Id = dc.Id,
                Name = dc.Name,
                IsDefault = dc.IsDefault
            })
            .ToListAsync();

        var selectedPlayer1ContextId = ResolveSelectedContextId(player1Contexts, query.Player1DataContextId);
        var selectedPlayer2ContextId = ResolveSelectedContextId(player2Contexts, query.Player2DataContextId);

        var player1ContextSummaries = await BuildContextSummariesAsync(context, player1Contexts);
        var player2ContextSummaries = await BuildContextSummariesAsync(context, player2Contexts);
        var player1CraftingTableSummaries = await BuildContextCraftingTableSummariesAsync(context, player1Contexts.Select(c => c.Id).ToList());
        var player2CraftingTableSummaries = await BuildContextCraftingTableSummariesAsync(context, player2Contexts.Select(c => c.Id).ToList());

        var comparisonRows = selectedPlayer1ContextId is null
            ? []
            : await BuildComparisonRowsAsync(context, query, selectedPlayer1ContextId.Value, selectedPlayer2ContextId);

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.Trim();
            comparisonRows = comparisonRows
                .Where(row =>
                    row.EntityName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(row.SecondaryLabel) && row.SecondaryLabel.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (query.OnlyDifferences)
        {
            comparisonRows = comparisonRows.Where(row => row.IsDifferent).ToList();
        }

        comparisonRows = SortComparisonRows(comparisonRows, query.SortBy, query.SortDescending).ToList();

        var totalComparisonCount = comparisonRows.Count;
        var pagedRows = applyPaging
            ? comparisonRows.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            : comparisonRows;

        return new EconomyPlayerDetailResult
        {
            Player1UserServerId = player1User.Id,
            Player2UserServerId = player2User.Id,
            Player1Name = player1User.PlayerName,
            Player2Name = player2User.PlayerName,
            Player1Contexts = player1Contexts,
            Player2Contexts = player2Contexts,
            SelectedPlayer1DataContextId = selectedPlayer1ContextId,
            SelectedPlayer2DataContextId = selectedPlayer2ContextId,
            Player1ContextSummaries = player1ContextSummaries,
            Player2ContextSummaries = player2ContextSummaries,
            Player1CraftingTableSummaries = player1CraftingTableSummaries,
            Player2CraftingTableSummaries = player2CraftingTableSummaries,
            ComparisonRows = pagedRows,
            TotalComparisonCount = totalComparisonCount,
            Page = applyPaging ? page : 1,
            PageSize = applyPaging ? pageSize : totalComparisonCount
        };
    }

    private static Guid? ResolveSelectedContextId(List<EconomyPlayerContextOption> options, Guid? requestedContextId)
    {
        if (options.Count == 0)
        {
            return null;
        }

        if (requestedContextId is Guid requestedId && options.Any(o => o.Id == requestedId))
        {
            return requestedId;
        }

        var defaultContext = options.FirstOrDefault(o => o.IsDefault);
        return defaultContext?.Id ?? options.First().Id;
    }

    private async Task<IEnumerable<EconomyGlobalRow>> BuildGlobalRowsByItemAsync(EcoCraftDbContext context, EconomyGlobalQuery query)
    {
        var facts = await GetPriceFactsAsync(context, query.ServerId);

        HashSet<Guid>? allowedItemOrTagIds = null;
        if (query.SkillId is Guid skillId)
        {
            var recipeFacts = await GetRecipeFactsAsync(context, query.ServerId);
            allowedItemOrTagIds = recipeFacts
                .Where(f => f.SkillId == skillId)
                .Select(f => f.ItemOrTagId)
                .ToHashSet();
        }

        return BuildGlobalRowsByItem(facts, query, allowedItemOrTagIds);
    }

    private static IEnumerable<EconomyGlobalRow> BuildGlobalRowsByItem(
        List<PriceFact> facts,
        EconomyGlobalQuery query,
        HashSet<Guid>? allowedItemOrTagIds)
    {
        var filteredFacts = facts.Where(f =>
                (allowedItemOrTagIds is null || allowedItemOrTagIds.Contains(f.ItemOrTagId)) &&
                (query.ItemOrTagId is null || f.ItemOrTagId == query.ItemOrTagId) &&
                (query.UserServerId is null || f.UserServerId == query.UserServerId) &&
                (string.IsNullOrWhiteSpace(query.SearchText) || f.ItemName.Contains(query.SearchText.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return filteredFacts
            .GroupBy(f => new { f.ItemOrTagId, f.ItemName, f.IsTag })
            .Select(group =>
            {
                var (priceMin, priceAverage, priceMax, marginMin, marginAverage, marginMax, configuredPlayersCount, configuredContextsCount, spread) =
                    ComputeMetrics(group.Select(g => (g.UserId, g.DataContextId, g.EffectivePrice, g.Margin)));

                return new EconomyGlobalRow
                {
                    GroupKey = group.Key.ItemOrTagId.ToString(),
                    Label = group.Key.ItemName,
                    SecondaryLabel = group.Key.IsTag ? "Tag" : "Item",
                    IsTag = group.Key.IsTag,
                    ItemOrTagId = group.Key.ItemOrTagId,
                    PriceMin = priceMin,
                    PriceAverage = priceAverage,
                    PriceMax = priceMax,
                    MarginMin = marginMin,
                    MarginAverage = marginAverage,
                    MarginMax = marginMax,
                    ConfiguredPlayersCount = configuredPlayersCount,
                    ConfiguredContextsCount = configuredContextsCount,
                    Spread = spread,
                    PlayerDetails = BuildGlobalItemPlayerDetails(group)
                };
            });
    }

    private static IEnumerable<EconomyGlobalRow> BuildGlobalRowsByPlayer(List<PriceFact> facts, EconomyGlobalQuery query)
    {
        return BuildGlobalRowsByPlayer(facts, query, null);
    }

    private async Task<IEnumerable<EconomyGlobalRow>> BuildGlobalRowsByPlayerAsync(EcoCraftDbContext context, EconomyGlobalQuery query)
    {
        var facts = await GetPriceFactsAsync(context, query.ServerId);

        HashSet<Guid>? allowedItemOrTagIds = null;
        if (query.SkillId is Guid skillId)
        {
            var recipeFacts = await GetRecipeFactsAsync(context, query.ServerId);
            allowedItemOrTagIds = recipeFacts
                .Where(f => f.SkillId == skillId)
                .Select(f => f.ItemOrTagId)
                .ToHashSet();
        }

        return BuildGlobalRowsByPlayer(facts, query, allowedItemOrTagIds);
    }

    private static IEnumerable<EconomyGlobalRow> BuildGlobalRowsByPlayer(
        List<PriceFact> facts,
        EconomyGlobalQuery query,
        HashSet<Guid>? allowedItemOrTagIds)
    {
        var filteredFacts = facts.Where(f =>
                (allowedItemOrTagIds is null || allowedItemOrTagIds.Contains(f.ItemOrTagId)) &&
                (query.ItemOrTagId is null || f.ItemOrTagId == query.ItemOrTagId) &&
                (query.UserServerId is null || f.UserServerId == query.UserServerId) &&
                (string.IsNullOrWhiteSpace(query.SearchText) || f.PlayerName.Contains(query.SearchText.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return filteredFacts
            .GroupBy(f => new { f.UserServerId, f.PlayerName })
            .Select(group =>
            {
                var (priceMin, priceAverage, priceMax, marginMin, marginAverage, marginMax, configuredPlayersCount, configuredContextsCount, spread) =
                    ComputeMetrics(group.Select(g => (g.UserId, g.DataContextId, g.EffectivePrice, g.Margin)));

                return new EconomyGlobalRow
                {
                    GroupKey = group.Key.UserServerId.ToString(),
                    Label = group.Key.PlayerName,
                    UserServerId = group.Key.UserServerId,
                    PriceMin = priceMin,
                    PriceAverage = priceAverage,
                    PriceMax = priceMax,
                    MarginMin = marginMin,
                    MarginAverage = marginAverage,
                    MarginMax = marginMax,
                    ConfiguredPlayersCount = configuredPlayersCount,
                    ConfiguredContextsCount = configuredContextsCount,
                    Spread = spread
                };
            });
    }

    private static IEnumerable<EconomyGlobalRow> BuildGlobalRowsByRecipe(List<RecipeFact> facts, EconomyGlobalQuery query)
    {
        var filteredFacts = facts.Where(f =>
                (query.ItemOrTagId is null || f.ItemOrTagId == query.ItemOrTagId) &&
                (query.SkillId is null || f.SkillId == query.SkillId) &&
                (query.UserServerId is null || f.UserServerId == query.UserServerId) &&
                (string.IsNullOrWhiteSpace(query.SearchText) || f.RecipeName.Contains(query.SearchText.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return filteredFacts
            .GroupBy(f => new { f.RecipeId, f.RecipeName, f.SkillName })
            .Select(group =>
            {
                var (priceMin, priceAverage, priceMax, marginMin, marginAverage, marginMax, configuredPlayersCount, configuredContextsCount, spread) =
                    ComputeMetrics(group.Select(g => (g.UserId, g.DataContextId, g.UnitPrice, g.Margin)));

                return new EconomyGlobalRow
                {
                    GroupKey = group.Key.RecipeId.ToString(),
                    Label = group.Key.RecipeName,
                    SecondaryLabel = group.Key.SkillName,
                    RecipeId = group.Key.RecipeId,
                    PriceMin = priceMin,
                    PriceAverage = priceAverage,
                    PriceMax = priceMax,
                    MarginMin = marginMin,
                    MarginAverage = marginAverage,
                    MarginMax = marginMax,
                    ConfiguredPlayersCount = configuredPlayersCount,
                    ConfiguredContextsCount = configuredContextsCount,
                    Spread = spread,
                    PlayerDetails = BuildGlobalRecipePlayerDetails(group)
                };
            });
    }

    private static IEnumerable<EconomyGlobalRow> BuildGlobalRowsBySkill(List<RecipeFact> facts, EconomyGlobalQuery query)
    {
        var filteredFacts = facts.Where(f =>
                (query.SkillId is null || f.SkillId == query.SkillId) &&
                (query.UserServerId is null || f.UserServerId == query.UserServerId) &&
                (string.IsNullOrWhiteSpace(query.SearchText) ||
                 (f.SkillName ?? "No Skill").Contains(query.SearchText.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return filteredFacts
            .GroupBy(f => new { f.SkillId, Label = f.SkillName ?? "No Skill" })
            .Select(group =>
            {
                var (priceMin, priceAverage, priceMax, marginMin, marginAverage, marginMax, configuredPlayersCount, configuredContextsCount, spread) =
                    ComputeMetrics(group.Select(g => (g.UserId, g.DataContextId, g.UnitPrice, g.Margin)));

                return new EconomyGlobalRow
                {
                    GroupKey = group.Key.SkillId?.ToString() ?? Guid.Empty.ToString(),
                    Label = group.Key.Label,
                    SkillId = group.Key.SkillId,
                    PriceMin = priceMin,
                    PriceAverage = priceAverage,
                    PriceMax = priceMax,
                    MarginMin = marginMin,
                    MarginAverage = marginAverage,
                    MarginMax = marginMax,
                    ConfiguredPlayersCount = configuredPlayersCount,
                    ConfiguredContextsCount = configuredContextsCount,
                    Spread = spread,
                    PlayerDetails = BuildGlobalPlayerDetails(group.Select(g => (
                        g.UserId,
                        g.UserServerId,
                        g.PlayerName,
                        g.DataContextId,
                        g.DataContextName,
                        g.IsDefaultContext,
                        g.UnitPrice,
                        g.Margin)))
                };
            });
    }

    private static (decimal? PriceMin, decimal? PriceAverage, decimal? PriceMax, decimal? MarginMin, decimal? MarginAverage, decimal? MarginMax, int ConfiguredPlayersCount, int ConfiguredContextsCount, decimal? Spread) ComputeMetrics(
        IEnumerable<(Guid UserId, Guid DataContextId, decimal? Price, decimal? Margin)> facts)
    {
        var list = facts.ToList();

        var priceValues = list.Where(f => f.Price is not null).Select(f => f.Price!.Value).ToList();
        var marginValues = list.Where(f => f.Margin is not null).Select(f => f.Margin!.Value).ToList();

        var configuredFacts = list.Where(f => f.Price is not null || f.Margin is not null).ToList();

        decimal? priceMin = priceValues.Count > 0 ? priceValues.Min() : null;
        decimal? priceAverage = priceValues.Count > 0 ? priceValues.Average() : null;
        decimal? priceMax = priceValues.Count > 0 ? priceValues.Max() : null;
        decimal? marginMin = marginValues.Count > 0 ? marginValues.Min() : null;
        decimal? marginAverage = marginValues.Count > 0 ? marginValues.Average() : null;
        decimal? marginMax = marginValues.Count > 0 ? marginValues.Max() : null;
        decimal? spread = priceMin is not null && priceMax is not null ? priceMax - priceMin : null;

        return (
            priceMin,
            priceAverage,
            priceMax,
            marginMin,
            marginAverage,
            marginMax,
            configuredFacts.Select(f => f.UserId).Distinct().Count(),
            configuredFacts.Select(f => f.DataContextId).Distinct().Count(),
            spread
        );
    }

    private static List<EconomyGlobalPlayerDetailRow> BuildGlobalPlayerDetails(
        IEnumerable<(Guid UserId, Guid UserServerId, string PlayerName, Guid DataContextId, string DataContextName, bool IsDefaultContext, decimal? Price, decimal? Margin)> facts)
    {
        return facts
            .GroupBy(f => new { f.UserServerId, f.PlayerName })
            .Select(group =>
            {
                var (priceMin, priceAverage, priceMax, marginMin, marginAverage, marginMax, _, configuredContextsCount, spread) =
                    ComputeMetrics(group.Select(g => (g.UserId, g.DataContextId, g.Price, g.Margin)));

                var (configuredPrice, configuredMargin) = ResolveConfiguredPriceAndMargin(
                    group.Select(g => (
                        g.DataContextId,
                        g.DataContextName,
                        g.IsDefaultContext,
                        g.Price,
                        g.Margin)));

                return new EconomyGlobalPlayerDetailRow
                {
                    UserServerId = group.Key.UserServerId,
                    PlayerName = group.Key.PlayerName,
                    ConfiguredPrice = configuredPrice,
                    ConfiguredMargin = configuredMargin,
                    PriceMin = priceMin,
                    PriceAverage = priceAverage,
                    PriceMax = priceMax,
                    MarginMin = marginMin,
                    MarginAverage = marginAverage,
                    MarginMax = marginMax,
                    ConfiguredContextsCount = configuredContextsCount,
                    Spread = spread
                };
            })
            .OrderBy(row => row.PlayerName)
            .ThenBy(row => row.UserServerId)
            .ToList();
    }

    private static List<EconomyGlobalPlayerDetailRow> BuildGlobalItemPlayerDetails(IEnumerable<PriceFact> facts)
    {
        return facts
            .GroupBy(f => new { f.UserServerId, f.PlayerName, f.DataContextId, f.DataContextName, f.IsDefaultContext })
            .Where(group => group.Any(entry => entry.EffectivePrice is not null || entry.Margin is not null))
            .Select(group =>
            {
                var (priceMin, priceAverage, priceMax, marginMin, marginAverage, marginMax, _, configuredContextsCount, spread) =
                    ComputeMetrics(group.Select(g => (g.UserId, g.DataContextId, g.EffectivePrice, g.Margin)));

                var (configuredPrice, configuredMargin) = ResolveConfiguredValues(
                    group.Select(g => (g.EffectivePrice, g.Margin)));

                var (primaryRecipeId, primaryRecipeName) = ResolvePrimaryRecipe(
                    group.Select(g => (g.PrimaryRecipeId, g.PrimaryRecipeName)));

                return new EconomyGlobalPlayerDetailRow
                {
                    UserServerId = group.Key.UserServerId,
                    PlayerName = group.Key.PlayerName,
                    DataContextId = group.Key.DataContextId,
                    DataContextName = group.Key.DataContextName,
                    IsDefaultContext = group.Key.IsDefaultContext,
                    PrimaryRecipeId = primaryRecipeId,
                    PrimaryRecipeName = primaryRecipeName,
                    ConfiguredPrice = configuredPrice,
                    ConfiguredMargin = configuredMargin,
                    PriceMin = priceMin,
                    PriceAverage = priceAverage,
                    PriceMax = priceMax,
                    MarginMin = marginMin,
                    MarginAverage = marginAverage,
                    MarginMax = marginMax,
                    ConfiguredContextsCount = configuredContextsCount,
                    Spread = spread
                };
            })
            .OrderBy(row => row.PlayerName)
            .ThenByDescending(row => row.IsDefaultContext)
            .ThenBy(row => row.DataContextName)
            .ThenBy(row => row.DataContextId)
            .ThenBy(row => row.UserServerId)
            .ToList();
    }

    private static List<EconomyGlobalPlayerDetailRow> BuildGlobalRecipePlayerDetails(IEnumerable<RecipeFact> facts)
    {
        return facts
            .GroupBy(f => new { f.UserServerId, f.PlayerName, f.DataContextId, f.DataContextName, f.IsDefaultContext })
            .Where(group => group.Any(entry => entry.UnitPrice is not null || entry.Margin is not null))
            .Select(group =>
            {
                var (priceMin, priceAverage, priceMax, marginMin, marginAverage, marginMax, _, configuredContextsCount, spread) =
                    ComputeMetrics(group.Select(g => (g.UserId, g.DataContextId, g.UnitPrice, g.Margin)));

                var (configuredPrice, configuredMargin) = ResolveConfiguredValues(
                    group.Select(g => (g.UnitPrice, g.Margin)));

                return new EconomyGlobalPlayerDetailRow
                {
                    UserServerId = group.Key.UserServerId,
                    PlayerName = group.Key.PlayerName,
                    DataContextId = group.Key.DataContextId,
                    DataContextName = group.Key.DataContextName,
                    IsDefaultContext = group.Key.IsDefaultContext,
                    ConfiguredPrice = configuredPrice,
                    ConfiguredMargin = configuredMargin,
                    PriceMin = priceMin,
                    PriceAverage = priceAverage,
                    PriceMax = priceMax,
                    MarginMin = marginMin,
                    MarginAverage = marginAverage,
                    MarginMax = marginMax,
                    ConfiguredContextsCount = configuredContextsCount,
                    Spread = spread
                };
            })
            .OrderBy(row => row.PlayerName)
            .ThenByDescending(row => row.IsDefaultContext)
            .ThenBy(row => row.DataContextName)
            .ThenBy(row => row.DataContextId)
            .ThenBy(row => row.UserServerId)
            .ToList();
    }

    private static (Guid? RecipeId, string? RecipeName) ResolvePrimaryRecipe(
        IEnumerable<(Guid? RecipeId, string? RecipeName)> primaryRecipes)
    {
        var candidates = primaryRecipes
            .Where(recipe => recipe.RecipeId is not null && !string.IsNullOrWhiteSpace(recipe.RecipeName))
            .Select(recipe => (RecipeId: recipe.RecipeId!.Value, RecipeName: recipe.RecipeName!.Trim()))
            .ToList();

        if (candidates.Count == 0)
        {
            return (null, null);
        }

        var best = candidates
            .GroupBy(recipe => new { recipe.RecipeId, recipe.RecipeName })
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.RecipeName)
            .ThenBy(group => group.Key.RecipeId)
            .Select(group => group.First())
            .First();

        return (best.RecipeId, best.RecipeName);
    }

    private static (decimal? Price, decimal? Margin) ResolveConfiguredPriceAndMargin(
        IEnumerable<(Guid DataContextId, string DataContextName, bool IsDefaultContext, decimal? Price, decimal? Margin)> facts)
    {
        var byContext = facts
            .GroupBy(f => new { f.DataContextId, f.DataContextName, f.IsDefaultContext })
            .Select(group =>
            {
                var prices = group
                    .Where(entry => entry.Price is not null)
                    .Select(entry => entry.Price!.Value)
                    .Distinct()
                    .ToList();

                var margins = group
                    .Where(entry => entry.Margin is not null)
                    .Select(entry => entry.Margin!.Value)
                    .Distinct()
                    .ToList();

                return new
                {
                    group.Key.DataContextId,
                    group.Key.DataContextName,
                    group.Key.IsDefaultContext,
                    Price = prices.Count == 1 ? prices[0] : (decimal?)null,
                    Margin = margins.Count == 1 ? margins[0] : (decimal?)null
                };
            })
            .OrderByDescending(context => context.IsDefaultContext)
            .ThenBy(context => context.DataContextName)
            .ThenBy(context => context.DataContextId)
            .ToList();

        if (byContext.Count == 0)
        {
            return (null, null);
        }

        var preferred = byContext[0];
        var configuredPrice = preferred.Price ?? byContext.Select(context => context.Price).FirstOrDefault(price => price is not null);
        var configuredMargin = preferred.Margin ?? byContext.Select(context => context.Margin).FirstOrDefault(margin => margin is not null);

        return (configuredPrice, configuredMargin);
    }

    private static (decimal? Price, decimal? Margin) ResolveConfiguredValues(
        IEnumerable<(decimal? Price, decimal? Margin)> facts)
    {
        var prices = facts
            .Where(entry => entry.Price is not null)
            .Select(entry => entry.Price!.Value)
            .Distinct()
            .ToList();

        var margins = facts
            .Where(entry => entry.Margin is not null)
            .Select(entry => entry.Margin!.Value)
            .Distinct()
            .ToList();

        return (
            prices.Count == 1 ? prices[0] : null,
            margins.Count == 1 ? margins[0] : null
        );
    }

    private static IEnumerable<EconomyGlobalRow> SortGlobalRows(IEnumerable<EconomyGlobalRow> rows, EconomyGlobalSortBy sortBy, bool descending)
    {
        return (sortBy, descending) switch
        {
            (EconomyGlobalSortBy.Label, false) => rows.OrderBy(r => r.Label),
            (EconomyGlobalSortBy.Label, true) => rows.OrderByDescending(r => r.Label),

            (EconomyGlobalSortBy.PriceMin, false) => rows.OrderBy(r => r.PriceMin ?? decimal.MaxValue),
            (EconomyGlobalSortBy.PriceMin, true) => rows.OrderByDescending(r => r.PriceMin ?? decimal.MinValue),

            (EconomyGlobalSortBy.PriceAverage, false) => rows.OrderBy(r => r.PriceAverage ?? decimal.MaxValue),
            (EconomyGlobalSortBy.PriceAverage, true) => rows.OrderByDescending(r => r.PriceAverage ?? decimal.MinValue),

            (EconomyGlobalSortBy.PriceMax, false) => rows.OrderBy(r => r.PriceMax ?? decimal.MaxValue),
            (EconomyGlobalSortBy.PriceMax, true) => rows.OrderByDescending(r => r.PriceMax ?? decimal.MinValue),

            (EconomyGlobalSortBy.MarginMin, false) => rows.OrderBy(r => r.MarginMin ?? decimal.MaxValue),
            (EconomyGlobalSortBy.MarginMin, true) => rows.OrderByDescending(r => r.MarginMin ?? decimal.MinValue),

            (EconomyGlobalSortBy.MarginAverage, false) => rows.OrderBy(r => r.MarginAverage ?? decimal.MaxValue),
            (EconomyGlobalSortBy.MarginAverage, true) => rows.OrderByDescending(r => r.MarginAverage ?? decimal.MinValue),

            (EconomyGlobalSortBy.MarginMax, false) => rows.OrderBy(r => r.MarginMax ?? decimal.MaxValue),
            (EconomyGlobalSortBy.MarginMax, true) => rows.OrderByDescending(r => r.MarginMax ?? decimal.MinValue),

            (EconomyGlobalSortBy.ConfiguredPlayers, false) => rows.OrderBy(r => r.ConfiguredPlayersCount),
            (EconomyGlobalSortBy.ConfiguredPlayers, true) => rows.OrderByDescending(r => r.ConfiguredPlayersCount),

            (EconomyGlobalSortBy.ConfiguredContexts, false) => rows.OrderBy(r => r.ConfiguredContextsCount),
            (EconomyGlobalSortBy.ConfiguredContexts, true) => rows.OrderByDescending(r => r.ConfiguredContextsCount),

            (EconomyGlobalSortBy.Spread, false) => rows.OrderBy(r => r.Spread ?? decimal.MaxValue),
            (EconomyGlobalSortBy.Spread, true) => rows.OrderByDescending(r => r.Spread ?? decimal.MinValue),
            _ => rows.OrderByDescending(r => r.Spread ?? decimal.MinValue)
        };
    }

    private static IEnumerable<EconomyPlayerComparisonRow> SortComparisonRows(IEnumerable<EconomyPlayerComparisonRow> rows, EconomyComparisonSortBy sortBy, bool descending)
    {
        return (sortBy, descending) switch
        {
            (EconomyComparisonSortBy.Entity, false) => rows.OrderBy(r => r.EntityName),
            (EconomyComparisonSortBy.Entity, true) => rows.OrderByDescending(r => r.EntityName),
            (EconomyComparisonSortBy.Player1Value, false) => rows.OrderBy(r => r.Player1Value ?? decimal.MaxValue),
            (EconomyComparisonSortBy.Player1Value, true) => rows.OrderByDescending(r => r.Player1Value ?? decimal.MinValue),
            (EconomyComparisonSortBy.Player2Value, false) => rows.OrderBy(r => r.Player2Value ?? decimal.MaxValue),
            (EconomyComparisonSortBy.Player2Value, true) => rows.OrderByDescending(r => r.Player2Value ?? decimal.MinValue),
            (EconomyComparisonSortBy.DeltaAbs, false) => rows.OrderBy(r => r.DeltaAbs ?? decimal.MaxValue),
            (EconomyComparisonSortBy.DeltaAbs, true) => rows.OrderByDescending(r => r.DeltaAbs ?? decimal.MinValue),
            (EconomyComparisonSortBy.DeltaPct, false) => rows.OrderBy(r => r.DeltaPct ?? decimal.MaxValue),
            (EconomyComparisonSortBy.DeltaPct, true) => rows.OrderByDescending(r => r.DeltaPct ?? decimal.MinValue),
            _ => rows.OrderByDescending(r => r.DeltaAbs ?? decimal.MinValue)
        };
    }

    private async Task<List<EconomyPlayerContextSummary>> BuildContextSummariesAsync(EcoCraftDbContext context, List<EconomyPlayerContextOption> contexts)
    {
        var contextIds = contexts.Select(c => c.Id).ToList();
        if (contextIds.Count == 0)
        {
            return [];
        }

        var priceFacts = await context.UserPrices
            .AsNoTracking()
            .Where(up => contextIds.Contains(up.DataContextId))
            .Select(up => new ContextPriceFact(
                up.DataContextId,
                up.MarginPrice ?? up.Price,
                up.UserMargin != null ? (decimal?)up.UserMargin.Margin : null))
            .ToListAsync();

        var recipeCounts = await context.UserRecipes
            .AsNoTracking()
            .Where(ur => contextIds.Contains(ur.DataContextId))
            .GroupBy(ur => ur.DataContextId)
            .Select(group => new RecipeCountFact(group.Key, group.Count()))
            .ToListAsync();

        var skillCounts = await context.UserSkills
            .AsNoTracking()
            .Where(us => contextIds.Contains(us.DataContextId) && us.SkillId != null)
            .GroupBy(us => us.DataContextId)
            .Select(group => new SkillCountFact(group.Key, group.Count()))
            .ToListAsync();

        var userSettingFacts = await context.UserSettings
            .AsNoTracking()
            .Where(us => contextIds.Contains(us.DataContextId))
            .Select(us => new ContextUserSettingFact(
                us.DataContextId,
                us.ApplyMarginBetweenSkills,
                us.MarginType,
                us.CalorieCost))
            .ToListAsync();

        var recipeCountByContext = recipeCounts.ToDictionary(k => k.DataContextId, v => v.RecipeCount);
        var skillCountByContext = skillCounts.ToDictionary(k => k.DataContextId, v => v.SkillCount);
        var userSettingByContext = userSettingFacts
            .GroupBy(s => s.DataContextId)
            .ToDictionary(g => g.Key, g => g.First());

        return contexts.Select(ctx =>
            {
                var scopedPrices = priceFacts.Where(p => p.DataContextId == ctx.Id).ToList();
                var configuredValues = scopedPrices.Where(p => p.EffectivePrice is not null).Select(p => p.EffectivePrice!.Value).ToList();
                var marginValues = scopedPrices.Where(p => p.Margin is not null).Select(p => p.Margin!.Value).ToList();
                userSettingByContext.TryGetValue(ctx.Id, out var userSetting);

                return new EconomyPlayerContextSummary
                {
                    DataContextId = ctx.Id,
                    DataContextName = ctx.Name,
                    ConfiguredItemCount = configuredValues.Count,
                    AveragePrice = configuredValues.Count > 0 ? configuredValues.Average() : null,
                    AverageMargin = marginValues.Count > 0 ? marginValues.Average() : null,
                    RecipeCount = recipeCountByContext.GetValueOrDefault(ctx.Id),
                    SkillCount = skillCountByContext.GetValueOrDefault(ctx.Id),
                    ApplyMarginBetweenSkills = userSetting?.ApplyMarginBetweenSkills,
                    MarginType = userSetting?.MarginType,
                    CalorieCost = userSetting?.CalorieCost
                };
            })
            .OrderByDescending(s => contexts.First(c => c.Id == s.DataContextId).IsDefault)
            .ThenBy(s => s.DataContextName)
            .ToList();
    }

    private async Task<List<EconomyPlayerCraftingTableSummary>> BuildContextCraftingTableSummariesAsync(EcoCraftDbContext context, List<Guid> contextIds)
    {
        if (contextIds.Count == 0)
        {
            return [];
        }

        var userCraftingTables = await context.UserCraftingTables
            .AsNoTracking()
            .Where(uct => contextIds.Contains(uct.DataContextId))
            .Include(uct => uct.CraftingTable)
            .Include(uct => uct.PluginModule)
            .Include(uct => uct.SkilledPluginModules)
            .ToListAsync();

        return userCraftingTables.Select(uct => new EconomyPlayerCraftingTableSummary
            {
                DataContextId = uct.DataContextId,
                CraftingTableId = uct.CraftingTableId,
                CraftingTableName = uct.CraftingTable.Name,
                PluginModuleId = uct.PluginModuleId,
                PluginModuleName = uct.PluginModule?.Name,
                SkilledPluginModuleIds = uct.SkilledPluginModules.Select(pm => pm.Id).ToList(),
                CraftMinuteFee = uct.CraftMinuteFee
            })
            .ToList();
    }

    private async Task<List<EconomyPlayerComparisonRow>> BuildComparisonRowsAsync(
        EcoCraftDbContext context,
        EconomyPlayerDetailQuery query,
        Guid selectedPlayer1ContextId,
        Guid? selectedPlayer2ContextId)
    {
        return query.ComparisonBy switch
        {
            EconomyComparisonEntity.Item => await BuildItemComparisonRowsAsync(context, selectedPlayer1ContextId, selectedPlayer2ContextId),
            EconomyComparisonEntity.Recipe => await BuildRecipeComparisonRowsAsync(context, selectedPlayer1ContextId, selectedPlayer2ContextId),
            EconomyComparisonEntity.Skill => await BuildSkillComparisonRowsAsync(context, selectedPlayer1ContextId, selectedPlayer2ContextId),
            _ => []
        };
    }

    private async Task<List<EconomyPlayerComparisonRow>> BuildItemComparisonRowsAsync(
        EcoCraftDbContext context,
        Guid player1DataContextId,
        Guid? player2DataContextId)
    {
        var contextIds = new List<Guid> { player1DataContextId };
        if (player2DataContextId is Guid player2Context)
        {
            contextIds.Add(player2Context);
        }

        var itemFacts = await context.UserPrices
            .AsNoTracking()
            .Where(up => contextIds.Contains(up.DataContextId))
            .Select(up => new
            {
                up.DataContextId,
                up.ItemOrTagId,
                up.ItemOrTag.Name,
                up.ItemOrTag.IsTag,
                Value = up.MarginPrice ?? up.Price,
                Margin = up.UserMargin != null ? (decimal?)up.UserMargin.Margin : null
            })
            .ToListAsync();

        return itemFacts
            .GroupBy(f => new { f.ItemOrTagId, f.Name, f.IsTag })
            .Select(group =>
            {
                var player1 = group.FirstOrDefault(g => g.DataContextId == player1DataContextId);
                var player2 = player2DataContextId is Guid player2ContextId ? group.FirstOrDefault(g => g.DataContextId == player2ContextId) : null;

                return BuildComparisonRow(
                    group.Key.ItemOrTagId.ToString(),
                    group.Key.Name,
                    group.Key.IsTag ? "Tag" : "Item",
                    player1?.Value,
                    player2?.Value,
                    player1?.Margin,
                    player2?.Margin,
                    itemOrTagId: group.Key.ItemOrTagId,
                    isTag: group.Key.IsTag);
            })
            .ToList();
    }

    private async Task<List<EconomyPlayerComparisonRow>> BuildRecipeComparisonRowsAsync(
        EcoCraftDbContext context,
        Guid player1DataContextId,
        Guid? player2DataContextId)
    {
        var contextIds = new List<Guid> { player1DataContextId };
        if (player2DataContextId is Guid player2Context)
        {
            contextIds.Add(player2Context);
        }

        var facts = await GetRecipeComparisonFactsAsync(context, contextIds);

        var byContextAndRecipe = facts
            .GroupBy(f => new { f.DataContextId, f.RecipeId, f.RecipeName, f.SkillId, f.SkillName })
            .Select(group => new
            {
                group.Key.DataContextId,
                group.Key.RecipeId,
                group.Key.RecipeName,
                group.Key.SkillId,
                group.Key.SkillName,
                Value = group.Where(g => g.UnitPrice is not null).Select(g => g.UnitPrice!.Value).DefaultIfEmpty().Average(),
                HasValue = group.Any(g => g.UnitPrice is not null),
                Margin = group.Where(g => g.Margin is not null).Select(g => g.Margin!.Value).DefaultIfEmpty().Average(),
                HasMargin = group.Any(g => g.Margin is not null)
            })
            .ToList();

        return byContextAndRecipe
            .GroupBy(g => new { g.RecipeId, g.RecipeName, g.SkillId, g.SkillName })
            .Select(group =>
            {
                var player1 = group.FirstOrDefault(g => g.DataContextId == player1DataContextId);
                var player2 = player2DataContextId is Guid player2ContextId ? group.FirstOrDefault(g => g.DataContextId == player2ContextId) : null;

                return BuildComparisonRow(
                    group.Key.RecipeId.ToString(),
                    group.Key.RecipeName,
                    group.Key.SkillName,
                    player1?.HasValue == true ? player1.Value : null,
                    player2?.HasValue == true ? player2.Value : null,
                    player1?.HasMargin == true ? player1.Margin : null,
                    player2?.HasMargin == true ? player2.Margin : null,
                    recipeId: group.Key.RecipeId,
                    skillId: group.Key.SkillId);
            })
            .ToList();
    }

    private async Task<List<EconomyPlayerComparisonRow>> BuildSkillComparisonRowsAsync(
        EcoCraftDbContext context,
        Guid player1DataContextId,
        Guid? player2DataContextId)
    {
        var contextIds = new List<Guid> { player1DataContextId };
        if (player2DataContextId is Guid player2Context)
        {
            contextIds.Add(player2Context);
        }

        var facts = await GetRecipeComparisonFactsAsync(context, contextIds);

        var byContextAndSkill = facts
            .GroupBy(f => new { f.DataContextId, f.SkillId, SkillName = f.SkillName ?? "No Skill" })
            .Select(group => new
            {
                group.Key.DataContextId,
                group.Key.SkillId,
                group.Key.SkillName,
                Value = group.Where(g => g.UnitPrice is not null).Select(g => g.UnitPrice!.Value).DefaultIfEmpty().Average(),
                HasValue = group.Any(g => g.UnitPrice is not null),
                Margin = group.Where(g => g.Margin is not null).Select(g => g.Margin!.Value).DefaultIfEmpty().Average(),
                HasMargin = group.Any(g => g.Margin is not null)
            })
            .ToList();

        return byContextAndSkill
            .GroupBy(g => new { g.SkillId, g.SkillName })
            .Select(group =>
            {
                var player1 = group.FirstOrDefault(g => g.DataContextId == player1DataContextId);
                var player2 = player2DataContextId is Guid player2ContextId ? group.FirstOrDefault(g => g.DataContextId == player2ContextId) : null;
                var key = group.Key.SkillId?.ToString() ?? "no-skill";

                return BuildComparisonRow(
                    key,
                    group.Key.SkillName,
                    null,
                    player1?.HasValue == true ? player1.Value : null,
                    player2?.HasValue == true ? player2.Value : null,
                    player1?.HasMargin == true ? player1.Margin : null,
                    player2?.HasMargin == true ? player2.Margin : null,
                    skillId: group.Key.SkillId);
            })
            .ToList();
    }

    private static EconomyPlayerComparisonRow BuildComparisonRow(
        string entityKey,
        string entityName,
        string? secondaryLabel,
        decimal? player1Value,
        decimal? player2Value,
        decimal? player1Margin,
        decimal? player2Margin,
        Guid? itemOrTagId = null,
        Guid? recipeId = null,
        Guid? skillId = null,
        bool? isTag = null)
    {
        decimal? deltaAbs = null;
        decimal? deltaPct = null;
        decimal? marginDelta = null;

        if (player1Value is not null && player2Value is not null)
        {
            deltaAbs = player2Value.Value - player1Value.Value;
            if (player1Value.Value != 0)
            {
                deltaPct = deltaAbs / player1Value.Value * 100;
            }
        }

        if (player1Margin is not null && player2Margin is not null)
        {
            marginDelta = player2Margin.Value - player1Margin.Value;
        }

        var status = GetComparisonStatus(player1Value, player2Value, deltaAbs);

        return new EconomyPlayerComparisonRow
        {
            EntityKey = entityKey,
            EntityName = entityName,
            SecondaryLabel = secondaryLabel,
            ItemOrTagId = itemOrTagId,
            RecipeId = recipeId,
            SkillId = skillId,
            IsTag = isTag,
            Player1Value = player1Value,
            Player2Value = player2Value,
            DeltaAbs = deltaAbs,
            DeltaPct = deltaPct,
            Player1Margin = player1Margin,
            Player2Margin = player2Margin,
            MarginDeltaAbs = marginDelta,
            Status = status,
            IsDifferent = status is not "Same" and not "NoData"
        };
    }

    private static string GetComparisonStatus(decimal? player1Value, decimal? player2Value, decimal? deltaAbs)
    {
        if (player1Value is null && player2Value is null)
        {
            return "NoData";
        }

        if (player1Value is not null && player2Value is null)
        {
            return "OnlyPlayer1";
        }

        if (player1Value is null && player2Value is not null)
        {
            return "OnlyPlayer2";
        }

        if (deltaAbs == 0)
        {
            return "Same";
        }

        return deltaAbs > 0 ? "Higher" : "Lower";
    }

    private async Task<List<PriceFact>> GetPriceFactsAsync(EcoCraftDbContext context, Guid serverId)
    {
        var priceFacts = await context.UserPrices
            .AsNoTracking()
            .Where(up => up.DataContext.UserServer.ServerId == serverId && !up.DataContext.IsShoppingList)
            .Select(up => new PriceFact(
                up.DataContext.UserServer.UserId,
                up.DataContext.UserServerId,
                up.DataContext.UserServer.Pseudo ?? up.DataContext.UserServer.User.Pseudo,
                up.DataContextId,
                up.DataContext.Name,
                up.DataContext.IsDefault,
                up.ItemOrTagId,
                up.ItemOrTag.Name,
                up.ItemOrTag.IsTag,
                up.MarginPrice ?? up.Price,
                up.UserMargin != null ? (decimal?)up.UserMargin.Margin : null,
                up.PrimaryUserElement != null ? up.PrimaryUserElement.Element.RecipeId : null,
                up.PrimaryUserElement != null ? up.PrimaryUserElement.Element.Recipe.Name : null
            ))
            .ToListAsync();

        var producedItemRecipeFacts = await context.UserElements
            .AsNoTracking()
            .Where(ue => ue.DataContext.UserServer.ServerId == serverId
                         && !ue.DataContext.IsShoppingList
                         && ue.Element.Quantity.BaseValue > 0
                         && !ue.IsReintegrated)
            .Select(ue => new ProducedItemRecipeFact(
                ue.DataContextId,
                ue.Element.ItemOrTagId,
                ue.Element.RecipeId,
                ue.Element.Recipe.Name))
            .ToListAsync();

        var fallbackRecipeByContextAndItem = producedItemRecipeFacts
            .Where(f => !string.IsNullOrWhiteSpace(f.RecipeName))
            .GroupBy(f => new { f.DataContextId, f.ItemOrTagId })
            .ToDictionary(
                group => (group.Key.DataContextId, group.Key.ItemOrTagId),
                group => group
                    .GroupBy(entry => new { entry.RecipeId, entry.RecipeName })
                    .OrderByDescending(recipeGroup => recipeGroup.Count())
                    .ThenBy(recipeGroup => recipeGroup.Key.RecipeName)
                    .ThenBy(recipeGroup => recipeGroup.Key.RecipeId)
                    .Select(recipeGroup => (RecipeId: (Guid?)recipeGroup.Key.RecipeId, RecipeName: (string?)recipeGroup.Key.RecipeName))
                    .First());

        return priceFacts
            .Select(priceFact =>
            {
                var recipeId = priceFact.PrimaryRecipeId;
                var recipeName = priceFact.PrimaryRecipeName;

                if (recipeId is null
                    && fallbackRecipeByContextAndItem.TryGetValue((priceFact.DataContextId, priceFact.ItemOrTagId), out var fallbackRecipe))
                {
                    recipeId = fallbackRecipe.RecipeId;
                    recipeName = fallbackRecipe.RecipeName;
                }

                return new PriceFact(
                    priceFact.UserId,
                    priceFact.UserServerId,
                    priceFact.PlayerName,
                    priceFact.DataContextId,
                    priceFact.DataContextName,
                    priceFact.IsDefaultContext,
                    priceFact.ItemOrTagId,
                    priceFact.ItemName,
                    priceFact.IsTag,
                    priceFact.EffectivePrice,
                    priceFact.Margin,
                    recipeId,
                    recipeName
                );
            })
            .ToList();
    }

    private async Task<List<RecipeFact>> GetRecipeFactsAsync(EcoCraftDbContext context, Guid serverId)
    {
        return await GetRecipeComparisonFactsAsync(context, serverId);
    }

    private async Task<List<RecipeFact>> GetRecipeComparisonFactsAsync(EcoCraftDbContext context, Guid serverId)
    {
        var facts =
            from ue in context.UserElements.AsNoTracking()
            join up in context.UserPrices.AsNoTracking()
                on new { ue.DataContextId, ue.Element.ItemOrTagId } equals new { up.DataContextId, up.ItemOrTagId } into upGroup
            from up in upGroup.DefaultIfEmpty()
            where ue.DataContext.UserServer.ServerId == serverId
                  && !ue.DataContext.IsShoppingList
                  && ue.Element.Quantity.BaseValue > 0
                  && !ue.IsReintegrated
            select new RecipeFact(
                ue.DataContext.UserServer.UserId,
                ue.DataContext.UserServerId,
                ue.DataContext.UserServer.Pseudo ?? ue.DataContext.UserServer.User.Pseudo,
                ue.DataContextId,
                ue.DataContext.Name,
                ue.DataContext.IsDefault,
                ue.Element.RecipeId,
                ue.Element.Recipe.Name,
                ue.Element.Recipe.SkillId,
                ue.Element.Recipe == null
                    ? null
                    : ue.Element.Recipe.Skill == null
                        ? null
                        : ue.Element.Recipe!.Skill!.Name,
                ue.Element.ItemOrTagId,
                ue.Price,
                up != null && up.UserMargin != null ? (decimal?)up.UserMargin.Margin : null
            );

        return await facts.ToListAsync();
    }

    private async Task<List<RecipeFact>> GetRecipeComparisonFactsAsync(EcoCraftDbContext context, List<Guid> contextIds)
    {
        var facts =
            from ue in context.UserElements.AsNoTracking()
            join up in context.UserPrices.AsNoTracking()
                on new { ue.DataContextId, ue.Element.ItemOrTagId } equals new { up.DataContextId, up.ItemOrTagId } into upGroup
            from up in upGroup.DefaultIfEmpty()
            where contextIds.Contains(ue.DataContextId)
                  && ue.Element.Quantity.BaseValue > 0
                  && !ue.IsReintegrated
            select new RecipeFact(
                ue.DataContext.UserServer.UserId,
                ue.DataContext.UserServerId,
                ue.DataContext.UserServer.Pseudo ?? ue.DataContext.UserServer.User.Pseudo,
                ue.DataContextId,
                ue.DataContext.Name,
                ue.DataContext.IsDefault,
                ue.Element.RecipeId,
                ue.Element.Recipe.Name,
                ue.Element.Recipe.SkillId,
                ue.Element.Recipe == null
                    ? null
                    : ue.Element.Recipe.Skill == null
                        ? null
                        : ue.Element.Recipe!.Skill!.Name,
                ue.Element.ItemOrTagId,
                ue.Price,
                up != null && up.UserMargin != null ? (decimal?)up.UserMargin.Margin : null
            );

        return await facts.ToListAsync();
    }

    private static async Task EnsureUserServerMembershipAsync(EcoCraftDbContext context, Guid serverId, Guid userServerId)
    {
        var hasAccess = await context.UserServers
            .AsNoTracking()
            .AnyAsync(us => us.Id == userServerId && us.ServerId == serverId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("This user is not a member of the selected server.");
        }
    }

    public static Guid? ResolvePlayer2Selection(
        Guid player1UserServerId,
        Guid? preferredPlayer2UserServerId,
        IEnumerable<UserServer> availablePlayers)
    {
        var players = availablePlayers as IList<UserServer> ?? availablePlayers.ToList();

        if (preferredPlayer2UserServerId is Guid preferredId
            && preferredId != player1UserServerId
            && players.Any(player => player.Id == preferredId))
        {
            return preferredId;
        }

        return GetFirstDifferentPlayerUserServerId(player1UserServerId, players);
    }

    public static Guid? GetFirstDifferentPlayerUserServerId(Guid player1UserServerId, IEnumerable<UserServer> availablePlayers)
    {
        return availablePlayers
            .Select(player => (Guid?)player.Id)
            .FirstOrDefault(playerId => playerId != player1UserServerId);
    }

    public static EconomyPlayerContextSummary? ResolveContextSummary(
        List<EconomyPlayerContextSummary> summaries,
        Guid? selectedContextId)
    {
        if (selectedContextId is null)
        {
            return null;
        }

        return summaries.FirstOrDefault(summary => summary.DataContextId == selectedContextId.Value);
    }

    public static HashSet<Guid> GetCraftingTableIdsFilteredByComparisonToggles(
        EconomyPlayerDetailResult drilldown,
        Guid? selectedPlayer1ContextId,
        Guid? selectedPlayer2ContextId,
        bool comparisonOnlyDifferences,
        bool comparisonOnlyCommon)
    {
        var player1CraftingTableIds = selectedPlayer1ContextId is null
            ? []
            : drilldown.Player1CraftingTableSummaries
                .Where(summary => summary.DataContextId == selectedPlayer1ContextId.Value)
                .Select(summary => summary.CraftingTableId)
                .ToHashSet();

        var player2CraftingTableIds = selectedPlayer2ContextId is null
            ? []
            : drilldown.Player2CraftingTableSummaries
                .Where(summary => summary.DataContextId == selectedPlayer2ContextId.Value)
                .Select(summary => summary.CraftingTableId)
                .ToHashSet();

        if (comparisonOnlyCommon)
        {
            player1CraftingTableIds.IntersectWith(player2CraftingTableIds);
            return player1CraftingTableIds;
        }

        if (comparisonOnlyDifferences)
        {
            player1CraftingTableIds.SymmetricExceptWith(player2CraftingTableIds);
            return player1CraftingTableIds;
        }

        player1CraftingTableIds.UnionWith(player2CraftingTableIds);
        return player1CraftingTableIds;
    }

    public static bool IsCommonComparisonRow(EconomyPlayerComparisonRow row)
    {
        return row.Player1Value is not null && row.Player2Value is not null;
    }

    public static bool IsNoDataComparisonRow(EconomyPlayerComparisonRow row)
    {
        return string.Equals(row.Status, "NoData", StringComparison.Ordinal);
    }
}

