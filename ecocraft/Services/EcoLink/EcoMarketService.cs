using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ecocraft.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.EcoLink;

public record MarketPriceInput(string Name, decimal? SellAverage, decimal SellQuantity, int SellShops, decimal? BuyAverage, decimal BuyQuantity, int BuyShops, decimal? TradedAverage, decimal TradedQuantity);
public record MarketSnapshotInput(string? Currency, int WindowDays, List<MarketPriceInput>? Items);

// Prix moyens du marché d'un serveur Eco, tirés de la route que le mod ajoute à son serveur web
// (GET /api/ecognome/v1/market). Requêtes signées HMAC avec un secret que le mod a donné à la liaison : il ne circule
// jamais, ce qui compte puisque le serveur web Eco parle en http. Résultat mis en cache dans ServerMarketPrice.
public class EcoMarketService(IDbContextFactory<EcoCraftDbContext> factory, IDataProtectionProvider dataProtection, IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "eco-market";
    public const string MarketPath = "/api/ecognome/v1/market";
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();
    readonly IDataProtector protector = dataProtection.CreateProtector("Server.EcoWebSecret");

    public static string? ResolveAddress(Server server) => !string.IsNullOrWhiteSpace(server.EcoWebAddressOverride) ? server.EcoWebAddressOverride : server.EcoWebUrl;

    public async Task<Dictionary<Guid, ServerMarketPrice>> GetCachedAsync(Guid serverId)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.ServerMarketPrices.Where(p => p.ServerId == serverId).ToDictionaryAsync(p => p.ItemOrTagId);
    }

    /// <summary>Stores where the mod's web server answers and the secret its requests are signed with.</summary>
    public async Task SaveEndpointAsync(Guid serverId, string webUrl, string secret)
    {
        await using var context = await factory.CreateDbContextAsync();
        var protectedSecret = protector.Protect(secret);
        await context.Servers.Where(s => s.Id == serverId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.EcoWebUrl, webUrl)
            .SetProperty(x => x.EcoWebSecretProtected, protectedSecret)
            .SetProperty(x => x.MarketLastFetchAttempt, (DateTimeOffset?)null)); // Nouvelle adresse : on retente tout de suite.
    }

    public async Task SetAddressOverrideAsync(Guid serverId, string? address)
    {
        await using var context = await factory.CreateDbContextAsync();
        var value = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        await context.Servers.Where(s => s.Id == serverId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.EcoWebAddressOverride, value)
            .SetProperty(x => x.MarketLastFetchAttempt, (DateTimeOffset?)null));
    }

    /// <summary>Pulls fresh prices unless the last attempt is recent. Returns the error of this attempt, null when it worked or was not due.</summary>
    public async Task<string?> RefreshAsync(Guid serverId, bool force = false)
    {
        await using var context = await factory.CreateDbContextAsync();
        var server = await context.Servers.AsNoTracking().FirstAsync(s => s.Id == serverId);

        if (!force && server.MarketLastFetchAttempt > DateTimeOffset.UtcNow - CacheLifetime) return null;
        if (ResolveAddress(server) is not { } address || server.EcoWebSecretProtected is null) return "The Eco server has not given its web address yet: it does on each start once connected.";

        var serverLock = Locks.GetOrAdd(serverId, _ => new SemaphoreSlim(1, 1));
        if (!await serverLock.WaitAsync(0)) return null; // Une lecture est déjà en cours pour ce serveur.

        try
        {
            await context.Servers.Where(s => s.Id == serverId).ExecuteUpdateAsync(s => s.SetProperty(x => x.MarketLastFetchAttempt, DateTimeOffset.UtcNow));

            var (snapshot, error) = await FetchAsync(address, protector.Unprotect(server.EcoWebSecretProtected), force ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(3));
            if (snapshot is not null) await SaveAsync(context, serverId, snapshot);

            await context.Servers.Where(s => s.Id == serverId).ExecuteUpdateAsync(s => s.SetProperty(x => x.MarketLastError, error));
            return error;
        }
        finally
        {
            serverLock.Release();
        }
    }

    async Task<(MarketSnapshotInput? Snapshot, string? Error)> FetchAsync(string address, string secret, TimeSpan timeout)
    {
        if (!Uri.TryCreate(address.TrimEnd('/') + MarketPath, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return (null, "Invalid Eco server address.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-EcoGnome-Timestamp", timestamp);
        request.Headers.Add("X-EcoGnome-Signature", Sign(secret, timestamp));

        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancel.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return (null, "The Eco server refused the signature: connect the server again from a store's Eco Gnome tab.");
            if (response.StatusCode == HttpStatusCode.NotFound) return (null, "The Eco server answered but has no Eco Gnome route: is the mod up to date?");
            if (!response.IsSuccessStatusCode) return (null, $"The Eco server answered {(int)response.StatusCode}.");

            var snapshot = await response.Content.ReadFromJsonAsync<MarketSnapshotInput>(JsonSerializerOptions.Web, cancel.Token);
            return snapshot is null ? (null, "Empty answer from the Eco server.") : (snapshot, null);
        }
        catch (OperationCanceledException) { return (null, "The Eco server did not answer in time (is its web server port open?)."); }
        catch (HttpRequestException e) when (e.InnerException is ForbiddenAddressException) { return (null, "This address is not allowed (private or local network)."); }
        catch (HttpRequestException) { return (null, "The Eco server is unreachable at this address."); }
        catch (JsonException) { return (null, "The Eco server sent an unreadable answer."); }
    }

    /// <summary>HMAC-SHA256 of method, path and timestamp: the mod recomputes it with its copy of the secret.</summary>
    public static string Sign(string secret, string timestamp) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"GET\n{MarketPath}\n{timestamp}")));

    static async Task SaveAsync(EcoCraftDbContext context, Guid serverId, MarketSnapshotInput snapshot)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var idsByName = await context.ItemOrTags
            .Where(i => i.ServerId == serverId && !i.IsTag)
            .Select(i => new { i.Id, i.Name })
            .ToDictionaryAsync(i => i.Name, i => i.Id);

        await context.ServerMarketPrices.Where(p => p.ServerId == serverId).ExecuteDeleteAsync();

        context.ServerMarketPrices.AddRange((snapshot.Items ?? [])
            .Where(i => idsByName.ContainsKey(i.Name))
            .DistinctBy(i => i.Name)
            .Select(i => new ServerMarketPrice
            {
                ServerId = serverId,
                ItemOrTagId = idsByName[i.Name],
                SellAverage = Round(i.SellAverage),
                SellQuantity = i.SellQuantity,
                SellShops = i.SellShops,
                BuyAverage = Round(i.BuyAverage),
                BuyQuantity = i.BuyQuantity,
                BuyShops = i.BuyShops,
                TradedAverage = Round(i.TradedAverage),
                TradedQuantity = i.TradedQuantity,
            }));
        await context.SaveChangesAsync();

        await context.Servers.Where(s => s.Id == serverId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.MarketCurrency, snapshot.Currency)
            .SetProperty(x => x.MarketWindowDays, snapshot.WindowDays)
            .SetProperty(x => x.MarketPricesUpdateTime, DateTimeOffset.UtcNow));

        await transaction.CommitAsync();
    }

    static decimal? Round(decimal? value) => value is { } v and > 0 ? Math.Round(v, 2, MidpointRounding.AwayFromZero) : null;

    // L'adresse vient du mod ou d'un admin : sans ce filtre, le site irait interroger son propre réseau (base de données,
    // services internes). Contrôlé à la connexion, sur l'IP réellement jointe, pour qu'un DNS ne puisse pas tromper le contrôle.
    public static async ValueTask<Stream> ConnectPublicOnlyAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var target = addresses.FirstOrDefault(IsPublic) ?? throw new ForbiddenAddressException();

        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast) return false;
        if (address.AddressFamily != AddressFamily.InterNetwork) return true;

        var b = address.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 127 || b[0] == 0 || b[0] >= 224
                 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                 || (b[0] == 192 && b[1] == 168)
                 || (b[0] == 169 && b[1] == 254)
                 || (b[0] == 100 && b[1] >= 64 && b[1] <= 127));
    }
}

public class ForbiddenAddressException() : Exception("Private or local addresses are not allowed.");
