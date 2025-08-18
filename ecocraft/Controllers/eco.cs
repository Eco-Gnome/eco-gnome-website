using ecocraft.Models;
using ecocraft.Services;
using ecocraft.Services.DbServices;
using Microsoft.AspNetCore.Mvc;

namespace ecocraft.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EcoController(
    EcoCraftDbContext dbContext,
    UserPriceDbService userPriceDbService,
    ServerDbService serverDbService,
    UserDbService userDbService,
    UserElementDbService userElementDbService,
    PriceCalculatorService priceCalculatorService,
    ItemOrTagDbService itemOrTagDbService
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
        var server = await serverDbService.GetByJoinCodeAsync(joinCode);

        if (server is null)
        {
            return BadRequest("Eco-Gnome server not found. Please create your server in Eco-Gnome and retrieve the correct server id from server-management page.");
        }

        var alreadyRegisterServer = await serverDbService.GetByEcoServerIdAsync(ecoServerId);

        if (alreadyRegisterServer is not null && server != alreadyRegisterServer)
        {
            return BadRequest($"Eco Server is already registered with an other Eco Gnome server: {alreadyRegisterServer.Name}.");
        }

        if (server.EcoServerId is not null && server.EcoServerId != ecoServerId)
        {
            return BadRequest("Eco Gnome Server is already registered with an other Eco Server.");
        }

        server.EcoServerId = ecoServerId;

        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("register-user")]
    public async Task<IActionResult> RegisterUser([FromQuery] string ecoServerId, [FromQuery] string userSecretId, [FromQuery] string ecoUserId, [FromQuery] string serverPseudo)
    {
        var server = await serverDbService.GetByEcoServerIdAsync(ecoServerId);

        if (server == null)
        {
            return BadRequest("Eco-Gnome Server not found. Please ask the admin to register the server first.");
        }

        Guid userSecretGuid;
        try
        {
            userSecretGuid = new Guid(userSecretId);
        }
        catch (Exception _)
        {
            return BadRequest("Invalid user-secret id. Must be a valid GUID.");
        }

        var user = await userDbService.GetBySecretIdAsync(userSecretGuid);

        if (user == null)
        {
            return BadRequest("Eco-Gnome User not found");
        }

        var userServer = user.UserServers.Find(us => us.Server.EcoServerId == ecoServerId);

        if (userServer == null)
        {
            return BadRequest("Please join the server first on Eco-Gnome thanks to the JoinCode provided by your server admin.");
        }

        if (userServer.EcoUserId is not null && userServer.EcoUserId != ecoUserId)
        {
            return BadRequest("Eco-Gnome User is already associated to an Eco user.");
        }

        userServer.EcoUserId = ecoUserId;
        userServer.Pseudo = serverPseudo;

        await dbContext.SaveChangesAsync();

        return Ok();
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

    [HttpGet("categories-items")]
    public async Task<IActionResult> GetCategoriesAndItems([FromQuery] string ecoServerId, [FromQuery] string ecoUserId, [FromQuery] string? context)
    {
        var result = await TryGetDataContext(ecoServerId, ecoUserId, context);

        if (result.Result is not null) return result.Result;
        var dataContext = result.Value!;

        await userPriceDbService.GetByDataContextForEcoApiAsync(dataContext);
        await userElementDbService.GetByDataContextForEcoApiAsync(dataContext);

        var items = priceCalculatorService.GetCategorizedItemOrTags(dataContext);

        var categoryToBuy = new EcoGnomeCategory(
            "Buy",
            OfferType.Buy,
            items.ToBuy.SelectMany(t => t.IsTag ? t.AssociatedItems : [t]).Distinct().Select(t => new EcoGnomeItem(
                t.Name,
                Math.Round(t.GetCurrentUserPrice(dataContext)?.GetMarginPriceOrPrice() ?? 0, 2, MidpointRounding.AwayFromZero)
            )).ToList()
        );

        var categoriesToSell = items.ToSell
            .GroupBy(i => i.Elements.FirstOrDefault()?.Recipe.SkillId ?? null)
            .Select(m => new EcoGnomeCategory(
                "Some Skill", // Not used in EcoGnomeMod for now
                OfferType.Sell,
                m.Select(i => new EcoGnomeItem(
                    i.Name,
                    Math.Round(i.GetCurrentUserPrice(dataContext)?.GetMarginPriceOrPrice() ?? 999999, 2, MidpointRounding.AwayFromZero)
                )).ToList()
            ));

        return Ok(categoriesToSell.Concat([categoryToBuy]));
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
            return BadRequest("ecoServerId and required and cannot be empty.");

        var server = await serverDbService.GetByEcoServerIdAsync(ecoServerId);
        if (server is null)
            return BadRequest("Can't find server.");

        return server;
    }

    private async Task<ActionResult<DataContext>> TryGetDataContext(string ecoServerId, string ecoUserId, string? dataContext)
    {
        if (string.IsNullOrWhiteSpace(ecoServerId) || string.IsNullOrWhiteSpace(ecoUserId))
            return BadRequest("ecoServerId and ecoUserId are required and cannot be empty.");

        var userServer = (await userDbService.GetUserServerByEcoIdsAsync(ecoUserId, ecoServerId)).FirstOrDefault();
        if (userServer is null)
            return BadRequest("Can't find user or server.");

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
