using System.Security.Claims;
using System.Text.RegularExpressions;
using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

public record DiscordIdentity(string Key, string Name, string? AvatarHash);

public static class DiscordAuth
{
    public const string Provider = "discord";
    const string AvatarHashClaim = "urn:discord:avatar:hash"; // Seul l'avatar est donné en hash : l'URL se reconstruit sur le CDN.

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Discord:ClientId"]) && !string.IsNullOrWhiteSpace(configuration["Discord:ClientSecret"]);

    public static DiscordIdentity? GetIdentity(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var key = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (key is null) return null;

        var avatarHash = principal.FindFirstValue(AvatarHashClaim);
        return new DiscordIdentity(key, principal.FindFirstValue(ClaimTypes.Name) ?? "", string.IsNullOrEmpty(avatarHash) ? null : avatarHash);
    }

    /// <summary>Avatar picture of a linked Discord account; Discord's default avatar when the user has none.</summary>
    public static string AvatarUrl(UserLogin login) => login.AvatarHash is { } hash
        ? $"https://cdn.discordapp.com/avatars/{login.ProviderKey}/{hash}.png?size=64"
        : $"https://cdn.discordapp.com/embed/avatars/{(ulong.TryParse(login.ProviderKey, out var id) ? (id >> 22) % 6 : 0)}.png";
}

// Connexions externes d'un compte. Discord n'est qu'un moyen de retrouver son compte sur un autre navigateur :
// sans connexion, le compte anonyme (UserId + SecretId du localStorage) fonctionne comme avant.
public partial class UserLoginService(IDbContextFactory<EcoCraftDbContext> factory)
{
    [GeneratedRegex(@"^user\d{8}$")]
    private static partial Regex GeneratedPseudo();

    public async Task<Guid?> FindUserIdAsync(string provider, string providerKey)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.UserLogins
            .Where(l => l.Provider == provider && l.ProviderKey == providerKey)
            .Select(l => (Guid?)l.UserId)
            .FirstOrDefaultAsync();
    }

    public async Task<UserLogin?> GetAsync(Guid userId, string provider)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.UserLogins.FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider);
    }

    /// <summary>Attaches the Discord login to the user. An auto-generated pseudo is replaced by the Discord name.</summary>
    public async Task AttachAsync(User user, DiscordIdentity identity)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.UserLogins.Add(new UserLogin
        {
            UserId = user.Id,
            Provider = DiscordAuth.Provider,
            ProviderKey = identity.Key,
            DisplayName = identity.Name,
            AvatarHash = identity.AvatarHash,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        if (GeneratedPseudo().IsMatch(user.Pseudo) && !string.IsNullOrWhiteSpace(identity.Name))
        {
            user.Pseudo = identity.Name;
            await context.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Pseudo, identity.Name));
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Keeps the stored Discord name and avatar in step with Discord: both can change between two logins.</summary>
    public async Task RefreshAsync(DiscordIdentity identity)
    {
        await using var context = await factory.CreateDbContextAsync();
        await context.UserLogins
            .Where(l => l.Provider == DiscordAuth.Provider && l.ProviderKey == identity.Key && (l.DisplayName != identity.Name || l.AvatarHash != identity.AvatarHash))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.DisplayName, identity.Name)
                .SetProperty(l => l.AvatarHash, identity.AvatarHash));
    }

    public async Task DetachAsync(Guid userId, string provider)
    {
        await using var context = await factory.CreateDbContextAsync();
        await context.UserLogins.Where(l => l.UserId == userId && l.Provider == provider).ExecuteDeleteAsync();
    }
}
