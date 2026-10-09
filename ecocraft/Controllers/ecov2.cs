using System.Collections.Concurrent;
using ecocraft.Models;
using ecocraft.Services.EcoLink;
using ecocraft.Services.ImportData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StableNameDotNet;

namespace ecocraft.Controllers;

// API du mod Eco authentifiée par jeton (Authorization: Bearer ...). Les jetons s'obtiennent par la liaison :
// le mod ouvre une demande (link/start), le joueur la confirme sur /link/{code}, le mod relève le jeton (link/status).
[ApiController]
[Route("api/eco/v2")]
public class EcoV2Controller(
    EcoLinkService ecoLinkService,
    EcoApiDataService ecoApiDataService,
    EcoMarketService ecoMarketService,
    ImportDataService importDataService
) : ControllerBase
{
    static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ImportLocks = new();

    // ---------- Liaison ----------

    [HttpPost("link/start")]
    [EnableRateLimiting(EcoLinkRateLimit.Policy)]
    public async Task<IActionResult> StartLink([FromBody] LinkStartRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.EcoServerId)) return BadRequest(Message("ecoServerId is required."));

        if (body.Kind == EcoLinkKind.User)
        {
            // Seul le serveur Eco relié peut ouvrir une liaison joueur : c'est lui qui garantit l'identité du joueur.
            if (string.IsNullOrWhiteSpace(body.EcoUserId)) return BadRequest(Message("ecoUserId is required."));
            if (await Authenticate(EcoLinkKind.Server) is not { } serverToken || serverToken.Server.EcoServerId != body.EcoServerId)
                return Unauthorized(Message("This Eco server is not connected to Eco Gnome yet. Ask an admin to connect it from a store's Eco Gnome tab."));
        }

        var (request, pollToken) = await ecoLinkService.StartAsync(body.Kind, body.EcoServerId, body.EcoServerName ?? "", body.EcoUserId, body.EcoUserName);
        return Ok(new LinkStartResponse(request.Code, $"/link/{request.Code}", pollToken, (int)EcoLinkService.RequestLifetime.TotalSeconds));
    }

    [HttpGet("link/status")]
    public async Task<IActionResult> LinkStatus([FromQuery] string pollToken)
    {
        if (string.IsNullOrWhiteSpace(pollToken)) return BadRequest(Message("pollToken is required."));

        var (status, token, request) = await ecoLinkService.PollAsync(pollToken);
        var accountName = token is not null && request is not null ? await ecoLinkService.GetLinkedAccountNameAsync(request) : null;
        return Ok(new LinkStatusResponse(status, token, request?.EcoServerName, accountName));
    }

    // ---------- Joueur ----------

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (await Authenticate(EcoLinkKind.User) is not { } token) return Unauthorized(NotLinked);

        var userServer = token.UserServer!;
        var contexts = userServer.DataContexts.Where(d => !d.IsShoppingList).OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name).Select(d => d.Name).ToList();
        return Ok(new MeResponse(userServer.User.Pseudo, token.Server.Name, contexts));
    }

    [HttpGet("prices")]
    public async Task<IActionResult> Prices([FromQuery] string? context)
    {
        if (await Authenticate(EcoLinkKind.User) is not { } token) return Unauthorized(NotLinked);
        var (dataContext, error) = EcoContextResolver.Resolve(token.UserServer!.DataContexts, context);
        if (dataContext is null) return BadRequest(Message(error!));

        var (toBuy, toSell) = await ecoApiDataService.LoadCategorizedAsync(dataContext);

        // Ce qu'Eco Gnome suit sans en connaître le prix part à null : l'offre de la boutique devient « sans prix ».
        // Ce qu'il ne suit pas du tout est absent, et la boutique n'y touche pas.
        var priced = dataContext.UserPrices
            .Where(up => up.GetMarginPriceOrPrice() is not null)
            .DistinctBy(up => up.ItemOrTag.Name)
            .ToDictionary(up => up.ItemOrTag.Name, up => EcoCategoryBuilder.RoundPrice(up.GetMarginPriceOrPrice()));
        var tracked = toBuy.Concat(toSell).Select(i => i.Name).Where(name => !priced.ContainsKey(name)).Distinct();

        return Ok(priced.Select(p => new EcoCategoryItem(p.Key, p.Value)).Concat(tracked.Select(name => new EcoCategoryItem(name, null))));
    }

    [HttpGet("shopping-list")]
    public async Task<IActionResult> ShoppingList([FromQuery] string? name)
    {
        if (await Authenticate(EcoLinkKind.User) is not { } token) return Unauthorized(NotLinked);

        var (list, error) = await ecoApiDataService.GetShoppingListAsync(token.UserServer!, name);
        return list is null ? BadRequest(Message(error!)) : Ok(list);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] string? context, [FromQuery] string filterSkill = "", [FromQuery] GroupBy groupBy = GroupBy.None)
    {
        if (await Authenticate(EcoLinkKind.User) is not { } token) return Unauthorized(NotLinked);
        var (dataContext, error) = EcoContextResolver.Resolve(token.UserServer!.DataContexts, context);
        if (dataContext is null) return BadRequest(Message(error!));

        var items = await ecoApiDataService.LoadCategorizedAsync(dataContext);
        return Ok(EcoCategoryBuilder.Build(items, dataContext, filterSkill, groupBy));
    }

    [HttpPost("buy-prices")]
    public async Task<IActionResult> BuyPrices([FromQuery] string? context, [FromBody] List<EcoCategoryItem> prices)
    {
        if (await Authenticate(EcoLinkKind.User) is not { } token) return Unauthorized(NotLinked);
        var (dataContext, error) = EcoContextResolver.Resolve(token.UserServer!.DataContexts, context);
        if (dataContext is null) return BadRequest(Message(error!));

        var (applied, ignored) = await ecoApiDataService.UpdateBuyPricesAsync(dataContext, prices.Where(p => p.Price is > 0).Select(p => (p.Name, p.Price!.Value)));
        return Ok(new BuyPricesResponse(applied, ignored));
    }

    // ---------- Serveur ----------

    [HttpGet("server/status")]
    public async Task<IActionResult> ServerStatus()
    {
        if (await Authenticate(EcoLinkKind.Server) is not { } token) return Unauthorized(NotLinked);
        var server = token.Server;
        return Ok(new ServerStatusResponse(server.Name, server.DataHash, server.LastDataUploadTime, server.MarketPricesUpdateTime));
    }

    [HttpPost("server/data")]
    [RequestSizeLimit(150_000_000)]
    public async Task<IActionResult> UploadData()
    {
        if (await Authenticate(EcoLinkKind.Server) is not { } token) return Unauthorized(NotLinked);

        // Le corps arrive en gzip (Content-Encoding), décompressé par le middleware RequestDecompression.
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();
        var hash = EcoLinkService.Hash(json);
        if (hash == token.Server.DataHash) return Ok(new UploadResponse("unchanged", "Data already up to date."));

        var importLock = ImportLocks.GetOrAdd(token.ServerId, _ => new SemaphoreSlim(1, 1));
        if (!await importLock.WaitAsync(0)) return Conflict(Message("An import is already running for this server."));

        try
        {
            var (errorCount, recipeErrorNames) = await importDataService.ImportServerData(json, token.Server);
            await ecoApiDataService.SetDataHashAsync(token.ServerId, hash);

            return Ok(errorCount == 0
                ? new UploadResponse("imported", "Import ok")
                : new UploadResponse("imported", $"Import successful but with warning: {errorCount} recipes have not been imported: " + recipeErrorNames.Join(",")));
        }
        catch (ImportException ex)
        {
            Console.WriteLine(ex);
            return BadRequest(Message("Upload Error: " + ex.Message));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return BadRequest(Message("UNEXPECTED_ERROR: " + ex.Message));
        }
        finally
        {
            importLock.Release();
        }
    }

    // Le mod annonce à chaque démarrage où joindre son serveur web et le secret de signature des requêtes de prix du marché.
    // Sans URL publique configurée côté Eco, l'adresse est l'IP d'où le mod appelle, avec le port du serveur web.
    [HttpPost("server/endpoint")]
    public async Task<IActionResult> ServerEndpoint([FromBody] ServerEndpointRequest body)
    {
        if (await Authenticate(EcoLinkKind.Server) is not { } token) return Unauthorized(NotLinked);
        if (string.IsNullOrWhiteSpace(body.Secret) || body.Secret.Length < 32) return BadRequest(Message("A secret of at least 32 characters is required."));

        var webUrl = !string.IsNullOrWhiteSpace(body.WebUrl) ? body.WebUrl.Trim() : CallerUrl(body.WebPort);
        if (webUrl is null) return BadRequest(Message("No web address given and the caller's address is unknown."));

        await ecoMarketService.SaveEndpointAsync(token.ServerId, webUrl, body.Secret);
        return Ok(new ServerEndpointResponse(webUrl));
    }

    string? CallerUrl(int port)
    {
        if (HttpContext.Connection.RemoteIpAddress is not { } ip || port is <= 0 or > 65535) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        var host = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{ip}]" : ip.ToString();
        return $"http://{host}:{port}";
    }

    // ---------- Utilitaires ----------

    static readonly MessageResponse NotLinked = new("Not connected to Eco Gnome. Use the Connect button in a store's Eco Gnome tab.");
    static MessageResponse Message(string message) => new(message);

    async Task<EcoApiToken?> Authenticate(EcoLinkKind kind)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;

        var token = await ecoLinkService.AuthenticateAsync(header["Bearer ".Length..].Trim());
        if (token?.Kind != kind) return null;

        // The account name as it is now: the mod shows it in the store tab and keeps it current from any call.
        if (token.UserServer?.User is { } user) Response.Headers["X-EcoGnome-Account"] = Uri.EscapeDataString(user.Pseudo);
        return token;
    }
}

public static class EcoLinkRateLimit
{
    public const string Policy = "eco-link";
}

public record LinkStartRequest(EcoLinkKind Kind, string EcoServerId, string? EcoServerName, string? EcoUserId, string? EcoUserName);
public record LinkStartResponse(string Code, string Path, string PollToken, int ExpiresInSeconds);
public record LinkStatusResponse(string Status, string? Token, string? ServerName, string? AccountName);
public record MeResponse(string Pseudo, string ServerName, List<string> Contexts);
public record BuyPricesResponse(int Applied, List<string> Ignored);
public record ServerStatusResponse(string ServerName, string? DataHash, DateTimeOffset? LastDataUploadTime, DateTimeOffset? MarketPricesUpdateTime);
public record UploadResponse(string Status, string Message);
public record ServerEndpointRequest(string? WebUrl, int WebPort, string Secret);
public record ServerEndpointResponse(string WebUrl);
public record MessageResponse(string Message);
