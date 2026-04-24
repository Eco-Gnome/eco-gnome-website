using ecocraft.Models;
using ecocraft.Services;
using ecocraft.Services.DbServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EcoController(
    UserPriceDbService userPriceDbService,
    ServerDbService serverDbService,
    UserDbService userDbService,
    IPriceCalculatorService priceCalculatorService,
    ItemOrTagDbService itemOrTagDbService,
    IDbContextFactory<EcoCraftDbContext> dbContextFactory
) : ControllerBase
{
    /*
     * Registrations:
     *   Concept: The Mod does not know about the registration process. The linking data is done on eco-gnome side.
     *   The Mod will only communicate with its ecoServerId and its ecoUserId
     *
     *   - Server Admin registers his server & himself
     *     - type command /eco-gnome register-server joinCode, userSecretId
     *     - the mod calls eco-gnome with /api/eco/register-server joinCode, ecoServerId
     *       - if the server is not found, throw error
     *       - if the server is already associated, throw error (TODO: add button in server management to dissociate)
     *       - otherwise, save the ecoServerId in the Server
     *
     *   - Player registers his userId:
     *     - User type command /eco-gnome register <userSecretId>
     *     - The mod calls eco-gnome with /api/eco/register-user ecoServerId, userSecretId, ecoUserId, serverPseudo
     *       - if the server is not found, throw error
     *       - if the user is not found, throw error
     *       - if the userServer does not exist, throw error (TODO: allow to join the server automatically)
     *       - if the userServer is already associated to another ecoUserId, throw error (TODO: add button in user management to dissociate)
     *       - otherwise, save the ecoUserId in the UserServer
     */

    [HttpGet("register-server")]
    public async Task<IActionResult> RegisterServer([FromQuery] string joinCode, [FromQuery] string ecoServerId)
    {
        var result = await serverDbService.RegisterServerAsync(joinCode, ecoServerId);

        return result.Result switch
        {
            RegisterServerResult.Success => Ok(),
            RegisterServerResult.EcoGnomeServerNotFound => BadRequest("Eco-Gnome server not found. Please create your server in Eco-Gnome and retrieve the correct server id from server-management page."),
            RegisterServerResult.EcoServerAlreadyRegisteredWithOtherEcoServer => BadRequest($"Eco Server is already registered with an other Eco Gnome server: {result.OtherServerName}."),
            RegisterServerResult.EcoGnomeServerAlreadyLinkedToOtherEcoServer => BadRequest("Eco Gnome Server is already registered with an other Eco Server."),
            _ => StatusCode(500)
        };
    }

    [HttpGet("register-user")]
    public async Task<IActionResult> RegisterUser([FromQuery] string ecoServerId, [FromQuery] string userSecretId, [FromQuery] string ecoUserId, [FromQuery] string serverPseudo)
    {
        var result = await userDbService.RegisterUserAsync(ecoServerId, userSecretId, ecoUserId, serverPseudo);

        return result switch
        {
            RegisterUserResult.Success => Ok(),
            RegisterUserResult.EcoServerNotFound => BadRequest("Eco-Gnome Server not found. Please ask the admin to register the server first."),
            RegisterUserResult.InvalidUserSecretId => BadRequest("Invalid user-secret id. Must be a valid GUID."),
            RegisterUserResult.EcoGnomeUserNotFound => BadRequest("Eco-Gnome User not found"),
            RegisterUserResult.UserNotInServer => BadRequest("Please join the server first on Eco-Gnome thanks to the JoinCode provided by your server admin."),
            RegisterUserResult.UserAlreadyAssociatedToAnotherEcoUser => BadRequest("Eco-Gnome User is already associated to an Eco user."),
            _ => StatusCode(500)
        };
    }

    [HttpGet("user-prices")]
    public async Task<IActionResult> GetUserPrices([FromQuery] string ecoServerId, [FromQuery] string ecoUserId, [FromQuery] string? context)
    {
        var result = await TryGetDataContext(ecoServerId, ecoUserId, context);
        if (result.Result is not null) return result.Result;
        var dataContext = result.Value!;

        var userPrices = await userPriceDbService.GetByDataContextForEcoApiAsync(dataContext, true);

        return Ok(userPrices.Select(up => new EcoGnomeItem(up.ItemOrTag.Name, Math.Round((decimal)up.GetMarginPriceOrPrice()!, 2, MidpointRounding.AwayFromZero))));
    }

    [HttpGet("categories-items-v2")]
    public async Task<IActionResult> GetCategoriesAndItemsV2([FromQuery] string ecoServerId, [FromQuery] string ecoUserId, [FromQuery] string filterSkill = "", [FromQuery] GroupBy groupBy = 0, [FromQuery] string? context = "")
    {
        var result = await GetItemsAndDataContext(ecoServerId, ecoUserId, context);
        if (result.Result is not null) return result.Result;

        var (items, dataContext) = result.Value;
        var filterSkills = filterSkill == "" ? [] : filterSkill.Split(',').ToList();

        var categoryToBuy = GetCategoryToBuy(items, dataContext, filterSkills);

        List<EcoGnomeCategory> categoriesToSell;
        var categories = items.ToSell;

        if (filterSkills.Count > 0)
        {
            categories = categories.Where(i =>
                i.GetAssociatedItemsAndSelf().Any(iot =>
                    iot.Elements.Any(e => e.Quantity is not null && e.IsProduct() && filterSkills.Contains(e.Recipe?.Skill?.Name)))
            ).ToList();
        }

        if (groupBy != GroupBy.None)
        {
            var groupedCategories = groupBy switch
            {
                GroupBy.Margin => categories.GroupBy(i => i.GetCurrentUserPrice(dataContext)?.UserMargin?.Name ?? null),
                GroupBy.Skill => categories.GroupBy(i => i.GetAssociatedItemsAndSelf().SelectMany(iot => iot.Elements).FirstOrDefault(e => e.Recipe?.Skill != null)?.Recipe.Skill?.Name),
                _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null)
            };

            categoriesToSell = groupedCategories.Select(m => new EcoGnomeCategory(
                groupBy switch { GroupBy.Margin => m.Key?.ToString() ?? "", GroupBy.Skill => m.Key?.ToString() ?? "", _ => "Production" },
                OfferType.Sell,
                m.Select(i => new EcoGnomeItem(
                    i.Name,
                    Math.Round(i.GetCurrentUserPrice(dataContext)?.GetMarginPriceOrPrice() ?? 999999, 2, MidpointRounding.AwayFromZero)
                )).ToList()
            )).ToList();
        }
        else
        {
            categoriesToSell = [new EcoGnomeCategory(
                filterSkill != "" ? filterSkill : "Production",
                OfferType.Sell,
                categories.Select(i => new EcoGnomeItem(
                    i.Name,
                    Math.Round(i.GetCurrentUserPrice(dataContext)?.GetMarginPriceOrPrice() ?? 999999, 2, MidpointRounding.AwayFromZero)
                )).ToList()
            )];
        }

        return Ok(categoriesToSell.Concat([categoryToBuy]));
    }

    private async Task<ActionResult<((List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) items, DataContext dataContext)>> GetItemsAndDataContext(string ecoServerId, string ecoUserId, string? context)
    {
        var result = await TryGetDataContext(ecoServerId, ecoUserId, context);

        if (result.Result is not null) return result.Result;
        var dataContext = result.Value!;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

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

        return (items, dataContext);
    }

    private static EcoGnomeCategory GetCategoryToBuy((List<ItemOrTag> ToBuy, List<ItemOrTag> ToSell) items, DataContext dataContext, List<string> filterSkills)
    {
        var toBuy = items.ToBuy;

        if (filterSkills.Count > 0)
        {
            toBuy = toBuy.Where(i =>
                i.GetAssociatedItemsAndSelf().Any(iot =>
                    iot.Elements.Any(e => e.Quantity is not null && e.IsIngredient() && filterSkills.Contains(e.Recipe?.Skill?.Name)))
            ).ToList();
        }

        return new EcoGnomeCategory(
            "Acquisition",
            OfferType.Buy,
            toBuy.SelectMany(t => t.IsTag ? t.GetAssociatedItemsAndSelf() : [t]).Distinct().Select(t => new EcoGnomeItem(
                t.Name,
                Math.Round(t.GetCurrentUserPrice(dataContext)?.GetMarginPriceOrPrice() ?? 0, 2, MidpointRounding.AwayFromZero)
            )).ToList()
        );
    }

    [HttpGet("server-prices")]
    public async Task<IActionResult> GetServerPrices([FromQuery] string ecoServerId)
    {
        var result = await TryGetServer(ecoServerId);
        if (result.Result is not null) return result.Result;
        var server = result.Value!;

        var itemOrTags = await itemOrTagDbService.GetWithPriceSetByServerAsync(server);

        return Ok(itemOrTags.Select(iot => new EcoGnomeServerPrice(
            iot.Name,
            iot.MinPrice is not null ? Math.Round((decimal)iot.MinPrice, 2, MidpointRounding.AwayFromZero) : null,
            iot.DefaultPrice is not null ? Math.Round((decimal)iot.DefaultPrice, 2, MidpointRounding.AwayFromZero): null,
            iot.MaxPrice is not null ? Math.Round((decimal)iot.MaxPrice, 2, MidpointRounding.AwayFromZero): null
        )));
    }

    private async Task<ActionResult<Server>> TryGetServer(string ecoServerId)
    {
        if (string.IsNullOrWhiteSpace(ecoServerId))
            return BadRequest("ecoServerId is required and cannot be empty.");

        var server = await serverDbService.GetByEcoServerIdAsync(ecoServerId);
        if (server is null)
            return BadRequest("Can't find server. Did you register your server thanks to /egserver <joinCode> ?");

        return server;
    }

    private async Task<ActionResult<DataContext>> TryGetDataContext(string ecoServerId, string ecoUserId, string? dataContext)
    {
        if (string.IsNullOrWhiteSpace(ecoServerId) || string.IsNullOrWhiteSpace(ecoUserId))
            return BadRequest("ecoServerId and ecoUserId are required and cannot be empty.");

        var userServer = (await userDbService.GetUserServerByEcoIdsAsync(ecoUserId, ecoServerId)).FirstOrDefault();
        if (userServer is null)
            return BadRequest("Can't find user or server. Did you register your user thanks to /eguser <secretId> ?");

        if (!string.IsNullOrWhiteSpace(dataContext))
        {
            var matches = userServer.DataContexts.Where(d => d.Name.StartsWith(dataContext)).ToList();

            if (matches.Count == 0)
                return BadRequest("No context starts with the name you provided. Leave it empty to use the default context.");

            if (matches.Count > 1)
                return BadRequest("Several contexts start with the name you provided. Please be more specific to select only one.");

            return matches.First();
        }

        return userServer.DataContexts.First(d => d.IsDefault);
    }
}

public enum OfferType
{
    All,
    Buy,
    Sell
}

public enum GroupBy
{
    None,
    Margin,
    Skill,
}

public class EcoGnomeCategory(string name, OfferType offerType, List<EcoGnomeItem> items)
{
    public string Name { get; set; } = name;
    public OfferType OfferType { get; set; } = offerType;
    public List<EcoGnomeItem> Items { get; set; } = items;
}

public class EcoGnomeServerPrice(string name, decimal? minPrice, decimal? defaultPrice, decimal? maxPrice)
{
    public string Name { get; set; } = name;
    public decimal? MinPrice { get; set; } = minPrice;
    public decimal? DefaultPrice { get; set; } = defaultPrice;
    public decimal? MaxPrice { get; set; } = maxPrice;
}

public class EcoGnomeItem(string name, decimal price, int minDurability = -1, int maxDurability = -1, int minIntegrity = -1, int maxIntegrity = -1)
{
    public string Name { get; set; } = name;
    public decimal Price { get; set; } = price;
    public int MinDurability { get; set; } = minDurability;
    public int MaxDurability { get; set; } = maxDurability;
    public int MinIntegrity { get; set; } = minIntegrity;
    public int MaxIntegrity { get; set; } = maxIntegrity;
}
