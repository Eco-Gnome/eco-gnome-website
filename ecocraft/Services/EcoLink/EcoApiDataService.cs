using ecocraft.Controllers;
using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.EcoLink;

// Données échangées avec le mod Eco : catégories achat/vente d'un contexte, prix d'achat envoyés depuis les boutiques.
public class EcoApiDataService(
    IDbContextFactory<EcoCraftDbContext> factory,
    PriceCalculatorService priceCalculatorService,
    UserPriceDbService userPriceDbService,
    ServerDbService serverDbService,
    DataContextDbService dataContextDbService,
    ShoppingListService shoppingListService)
{
    /// <summary>Loads the context's prices and elements and splits its items into what the user buys and what they sell.</summary>
    public async Task<(List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell)> LoadCategorizedAsync(DataContext dataContext)
    {
        await using var dbContext = await factory.CreateDbContextAsync();

        dataContext.UserPrices.AddRange(await dbContext.UserPrices
            .Where(up => up.DataContextId == dataContext.Id)
            .Include(up => up.ItemOrTag)
                .ThenInclude(i => i.AssociatedItems)
            .Include(up => up.ItemOrTag)
                .ThenInclude(i => i.Elements)
                .ThenInclude(e => e.Quantity)
            .Include(up => up.UserMargin)
            .ToListAsync());

        dataContext.UserElements.AddRange(await dbContext.UserElements
            .Where(ue => ue.DataContextId == dataContext.Id)
            .Include(ue => ue.Element)
                .ThenInclude(e => e.Recipe)
                .ThenInclude(r => r.Skill)
                .ThenInclude(s => s!.LocalizedName)
            .Include(ue => ue.Element)
                .ThenInclude(e => e.ItemOrTag)
            .Include(ue => ue.Element)
                .ThenInclude(e => e.Quantity)
            .ToListAsync());

        var items = priceCalculatorService.GetCategorizedItemOrTags(dataContext);

        // Include tags that have a calculated price
        var tagsWithPrice = dataContext.UserPrices
            .Where(up => up.ItemOrTag.IsTag && up.GetMarginPriceOrPrice() is not null)
            .Select(up => up.ItemOrTag)
            .ToList();

        var sellTags = tagsWithPrice.Where(t => t.AssociatedItems.Intersect(items.ToSell).Any()).Except(items.ToSell).ToList();
        var buyTags = tagsWithPrice.Where(t => t.AssociatedItems.Intersect(items.ToBuy).Any()).Except(items.ToBuy).ToList();

        items.ToSell.AddRange(sellTags);
        items.ToBuy.AddRange(buyTags);

        return items;
    }

    /// <summary>Writes store buy prices into the prices the user types by hand: items they buy only, never computed sell prices.
    /// A tag price (a store buying "any log") goes to the tag's items the user buys, as the calculator prices a tag through its items;
    /// an offer on a precise item wins over its tag.</summary>
    public async Task<(int Applied, List<string> Ignored)> UpdateBuyPricesAsync(DataContext dataContext, IEnumerable<(string Name, decimal Price)> prices)
    {
        var (toBuy, _) = await LoadCategorizedAsync(dataContext);

        // Items a buy price can land on: those bought directly, and the items of the tags bought (a recipe asking for a tag takes any of them).
        var buyableItems = toBuy
            .SelectMany(i => i.IsTag ? i.AssociatedItems : [i])
            .Where(i => !i.IsTag)
            .DistinctBy(i => i.Id)
            .ToDictionary(i => i.Name);
        var tags = dataContext.UserPrices
            .Select(up => up.ItemOrTag)
            .Where(i => i.IsTag)
            .DistinctBy(i => i.Name)
            .ToDictionary(i => i.Name);
        var userPrices = dataContext.UserPrices.DistinctBy(up => up.ItemOrTagId).ToDictionary(up => up.ItemOrTagId);

        var newPrices = new Dictionary<Guid, (ItemOrTag Item, decimal Price)>();
        var ignored = new List<string>();
        var received = prices.ToList();

        foreach (var (name, price) in received.Where(p => tags.ContainsKey(p.Name)))
        {
            var targets = tags[name].AssociatedItems.Where(i => buyableItems.ContainsKey(i.Name)).ToList();
            if (targets.Count == 0) { ignored.Add(name); continue; }
            foreach (var item in targets) newPrices[item.Id] = (item, price);
        }

        foreach (var (name, price) in received.Where(p => !tags.ContainsKey(p.Name)))
        {
            if (!buyableItems.TryGetValue(name, out var item)) { ignored.Add(name); continue; }
            newPrices[item.Id] = (item, price);
        }

        var changed = new List<UserPrice>();
        foreach (var (item, price) in newPrices.Values)
        {
            if (!userPrices.TryGetValue(item.Id, out var userPrice)) { ignored.Add(item.Name); continue; }

            var clamped = Math.Round(Math.Clamp(price, item.MinPrice ?? decimal.MinValue, item.MaxPrice ?? decimal.MaxValue), 2, MidpointRounding.AwayFromZero);
            if (userPrice.Price == clamped) continue;

            userPrice.Price = clamped;
            changed.Add(userPrice);
        }

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            foreach (var userPrice in changed) userPriceDbService.UpdatePrice(context, userPrice);
            return Task.CompletedTask;
        });

        return (changed.Count, ignored);
    }

    /// <summary>One shopping list of the user with its items to buy, or only the names of their lists when <paramref name="name"/> is empty.</summary>
    public async Task<(EcoGnomeShoppingList? List, string? Error)> GetShoppingListAsync(UserServer userServer, string? name)
    {
        var shoppingLists = userServer.DataContexts.Where(d => d.IsShoppingList).ToList();
        if (string.IsNullOrWhiteSpace(name))
            return (new EcoGnomeShoppingList("", shoppingLists.Select(d => d.Name).ToList(), []), null);

        // An exact name wins, so "Forge" can still be picked next to "Forge 2"
        var matches = shoppingLists.Where(d => d.Name == name).ToList();
        if (matches.Count == 0) matches = shoppingLists.Where(d => d.Name.StartsWith(name)).ToList();

        if (matches.Count == 0) return (null, "No shopping list starts with the name you provided.");
        if (matches.Count > 1) return (null, "Several shopping lists start with the name you provided. Please be more specific to select only one.");

        var serverData = await serverDbService.GetServerWithShoppingListData(userServer.ServerId);
        var shoppingList = await dataContextDbService.GetDataContextWithData(matches[0].Id, serverData);

        // Negative outputs are the "Items to buy" of the shopping list page
        var items = shoppingListService.GetAggregatedOutputs(shoppingList, shoppingList.GetRootShoppingListRecipes())
            .Where(o => o.Value < 0)
            .Select(o => new EcoGnomeShoppingItem(o.Key.Name, o.Key.IsTag, (int)Math.Ceiling(Math.Round(Math.Abs(o.Value), 4))))
            .ToList();

        return (new EcoGnomeShoppingList(shoppingList.Name, [], items), null);
    }

    public async Task SetDataHashAsync(Guid serverId, string hash)
    {
        await using var context = await factory.CreateDbContextAsync();
        await context.Servers.Where(s => s.Id == serverId).ExecuteUpdateAsync(s => s.SetProperty(x => x.DataHash, hash));
    }
}
