using System.Security.Cryptography;
using System.Text;
using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.EcoLink;

public enum EcoLinkConfirmResult
{
    Success,
    NotFound,
    Expired,
    ServerNotLinked,
    EcoServerOwnedByAnotherServer,
    NotServerAdmin,
}

// Liaison Eco <-> Eco Gnome sans copier de code : le mod ouvre une demande, le joueur la confirme sur /link/{code},
// le mod relève un jeton d'API. Jetons et PollToken ne sont stockés que hachés.
public class EcoLinkService(IDbContextFactory<EcoCraftDbContext> factory)
{
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);
    const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Sans 0/O ni 1/I : le code se lit dans l'URL.

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    static string RandomToken(string prefix) => prefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static string RandomCode() => new(Enumerable.Range(0, 8).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());

    public async Task<(EcoLinkRequest Request, string PollToken)> StartAsync(EcoLinkKind kind, string ecoServerId, string ecoServerName, string? ecoUserId, string? ecoUserName)
    {
        await using var context = await factory.CreateDbContextAsync();

        // Les demandes expirées n'ont plus d'usage : on les purge au passage plutôt qu'avec une tâche planifiée.
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await context.EcoLinkRequests.Where(r => r.ExpiresAt < cutoff).ExecuteDeleteAsync();

        var pollToken = RandomToken("egp_");
        var request = new EcoLinkRequest
        {
            Code = RandomCode(),
            PollTokenHash = Hash(pollToken),
            Kind = kind,
            Status = EcoLinkStatus.Pending,
            EcoServerId = ecoServerId,
            EcoServerName = Truncate(ecoServerName, 100),
            EcoUserId = ecoUserId,
            EcoUserName = ecoUserName is null ? null : Truncate(ecoUserName, 100),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RequestLifetime),
        };

        context.EcoLinkRequests.Add(request);
        await context.SaveChangesAsync();

        return (request, pollToken);
    }

    static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    public async Task<EcoLinkRequest?> GetPendingByCodeAsync(string code)
    {
        await using var context = await factory.CreateDbContextAsync();
        var request = await context.EcoLinkRequests.FirstOrDefaultAsync(r => r.Code == code.ToUpperInvariant());
        return request is { Status: EcoLinkStatus.Pending } && request.ExpiresAt > DateTimeOffset.UtcNow ? request : null;
    }

    /// <summary>Confirms a player link: the user's membership of the Eco Gnome server now stands for this Eco player.</summary>
    public async Task<EcoLinkConfirmResult> ConfirmUserAsync(Guid requestId, UserServer userServer)
    {
        await using var context = await factory.CreateDbContextAsync();
        var request = await context.EcoLinkRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (CheckPending(request) is { } failure) return failure;

        // Un joueur Eco n'appartient qu'à un compte Eco Gnome par serveur : l'ancien lien est rompu, ses jetons révoqués.
        var previous = await context.UserServers
            .Where(us => us.ServerId == userServer.ServerId && us.EcoUserId == request!.EcoUserId && us.Id != userServer.Id)
            .ToListAsync();

        foreach (var other in previous)
        {
            other.EcoUserId = null;
            other.Pseudo = null;
        }

        var revokedIds = previous.Select(us => us.Id).Append(userServer.Id).ToList();
        await RevokeUserTokensAsync(context, revokedIds);

        var target = await context.UserServers.Include(us => us.User).FirstAsync(us => us.Id == userServer.Id);
        target.EcoUserId = request!.EcoUserId;
        target.Pseudo = request.EcoUserName;
        target.User.LastActionDateTime = DateTimeOffset.UtcNow;

        request.Status = EcoLinkStatus.Confirmed;
        request.ConfirmedServerId = userServer.ServerId;
        request.ConfirmedUserServerId = userServer.Id;

        await context.SaveChangesAsync();

        userServer.EcoUserId = target.EcoUserId;
        userServer.Pseudo = target.Pseudo;
        return EcoLinkConfirmResult.Success;
    }

    /// <summary>Confirms a server link onto an Eco Gnome server the user administrates.</summary>
    public async Task<(EcoLinkConfirmResult Result, string? OtherServerName)> ConfirmServerAsync(Guid requestId, UserServer adminUserServer, IReadOnlyCollection<Guid> serversUserAdministrates)
    {
        await using var context = await factory.CreateDbContextAsync();
        var request = await context.EcoLinkRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (CheckPending(request) is { } failure) return (failure, null);
        if (!adminUserServer.IsAdmin) return (EcoLinkConfirmResult.NotServerAdmin, null);

        // Le serveur Eco déjà relié ailleurs ne bascule que si l'utilisateur administre aussi l'autre serveur Eco Gnome.
        var owner = await context.Servers.FirstOrDefaultAsync(s => s.EcoServerId == request!.EcoServerId && s.Id != adminUserServer.ServerId);
        if (owner is not null)
        {
            if (!serversUserAdministrates.Contains(owner.Id)) return (EcoLinkConfirmResult.EcoServerOwnedByAnotherServer, owner.Name);
            owner.EcoServerId = null;
            await RevokeServerTokensAsync(context, owner.Id);
        }

        // Relier à nouveau le même serveur Eco (config du mod perdue) ne coupe pas les joueurs déjà liés.
        var server = await context.Servers.FirstAsync(s => s.Id == adminUserServer.ServerId);
        await RevokeServerTokensAsync(context, server.Id, onlyServerKind: server.EcoServerId == request!.EcoServerId);
        server.EcoServerId = request.EcoServerId;

        request.Status = EcoLinkStatus.Confirmed;
        request.ConfirmedServerId = server.Id;
        request.ConfirmedUserServerId = adminUserServer.Id;

        await context.SaveChangesAsync();
        return (EcoLinkConfirmResult.Success, null);
    }

    public async Task RejectAsync(Guid requestId)
    {
        await using var context = await factory.CreateDbContextAsync();
        await context.EcoLinkRequests
            .Where(r => r.Id == requestId && r.Status == EcoLinkStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, EcoLinkStatus.Rejected));
    }

    static EcoLinkConfirmResult? CheckPending(EcoLinkRequest? request) => request switch
    {
        null => EcoLinkConfirmResult.NotFound,
        { Status: not EcoLinkStatus.Pending } => EcoLinkConfirmResult.Expired,
        _ when request.ExpiresAt <= DateTimeOffset.UtcNow => EcoLinkConfirmResult.Expired,
        _ => null,
    };

    /// <summary>Pseudo of the Eco Gnome account a confirmed player link went to, shown in game as "Connected as ...".</summary>
    public async Task<string?> GetLinkedAccountNameAsync(EcoLinkRequest request)
    {
        if (request.ConfirmedUserServerId is not { } userServerId) return null;
        await using var context = await factory.CreateDbContextAsync();
        return await context.UserServers.Where(us => us.Id == userServerId).Select(us => us.User.Pseudo).FirstOrDefaultAsync();
    }

    /// <summary>What the mod sees when it polls. A confirmed request hands out its token exactly once.</summary>
    public async Task<(string Status, string? Token, EcoLinkRequest? Request)> PollAsync(string pollToken)
    {
        await using var context = await factory.CreateDbContextAsync();
        var hash = Hash(pollToken);
        var request = await context.EcoLinkRequests.FirstOrDefaultAsync(r => r.PollTokenHash == hash);

        if (request is null) return ("unknown", null, null);
        if (request.Status == EcoLinkStatus.Rejected) return ("rejected", null, request);
        if (request.Status == EcoLinkStatus.Consumed) return ("expired", null, request);
        if (request.Status == EcoLinkStatus.Pending) return (request.ExpiresAt > DateTimeOffset.UtcNow ? "pending" : "expired", null, request);

        // Deux relèves simultanées : seule celle qui fait passer la demande à Consumed reçoit le jeton.
        var claimed = await context.EcoLinkRequests
            .Where(r => r.Id == request.Id && r.Status == EcoLinkStatus.Confirmed)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, EcoLinkStatus.Consumed));
        if (claimed != 1) return ("expired", null, request);

        var token = RandomToken(request.Kind == EcoLinkKind.Server ? "egs_" : "egu_");
        context.EcoApiTokens.Add(new EcoApiToken
        {
            TokenHash = Hash(token),
            Kind = request.Kind,
            ServerId = request.ConfirmedServerId!.Value,
            UserServerId = request.Kind == EcoLinkKind.User ? request.ConfirmedUserServerId : null,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        return ("confirmed", token, request);
    }

    /// <summary>Resolves a bearer token, with its server and (for player tokens) its membership and contexts.</summary>
    public async Task<EcoApiToken?> AuthenticateAsync(string token)
    {
        await using var context = await factory.CreateDbContextAsync();
        var hash = Hash(token);
        var apiToken = await context.EcoApiTokens
            .Include(t => t.Server)
            .Include(t => t.UserServer)
                .ThenInclude(us => us!.DataContexts)
            .Include(t => t.UserServer)
                .ThenInclude(us => us!.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);

        if (apiToken is null) return null;

        // Un serveur dissocié ou un joueur délié invalide ses jetons, même si la révocation a été manquée.
        if (apiToken.Server.EcoServerId is null) return null;
        if (apiToken.Kind == EcoLinkKind.User && apiToken.UserServer?.EcoUserId is null) return null;

        // Date de dernière utilisation : une écriture par heure au plus, pas une par requête.
        if (apiToken.LastUsedAt is null || apiToken.LastUsedAt < DateTimeOffset.UtcNow.AddHours(-1))
            await context.EcoApiTokens.Where(t => t.Id == apiToken.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, DateTimeOffset.UtcNow));

        return apiToken;
    }

    public async Task RevokeUserTokensAsync(IEnumerable<Guid> userServerIds)
    {
        await using var context = await factory.CreateDbContextAsync();
        await RevokeUserTokensAsync(context, userServerIds.ToList());
    }

    public async Task RevokeServerTokensAsync(Guid serverId)
    {
        await using var context = await factory.CreateDbContextAsync();
        await RevokeServerTokensAsync(context, serverId);
    }

    static Task RevokeUserTokensAsync(EcoCraftDbContext context, List<Guid> userServerIds) =>
        context.EcoApiTokens
            .Where(t => t.UserServerId != null && userServerIds.Contains(t.UserServerId.Value) && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow));

    // Dissocier un serveur coupe le serveur Eco et tous les joueurs qui passaient par lui.
    static Task RevokeServerTokensAsync(EcoCraftDbContext context, Guid serverId, bool onlyServerKind = false) =>
        context.EcoApiTokens
            .Where(t => t.ServerId == serverId && t.RevokedAt == null && (!onlyServerKind || t.Kind == EcoLinkKind.Server))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow));
}
