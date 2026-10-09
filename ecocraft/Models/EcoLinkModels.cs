using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ecocraft.Models;

public enum EcoLinkKind
{
    User,
    Server,
}

public enum EcoLinkStatus
{
    Pending,
    Confirmed,
    Rejected,
    Consumed,
}

// Demande de liaison ouverte par le mod : le joueur la confirme sur /link/{Code}, le mod la relève par son PollToken.
// Le jeton d'API n'est créé qu'à la relève, il n'est donc jamais stocké en clair.
public class EcoLinkRequest
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string PollTokenHash { get; set; } = "";
    public EcoLinkKind Kind { get; set; }
    public EcoLinkStatus Status { get; set; }
    public string EcoServerId { get; set; } = "";
    public string EcoServerName { get; set; } = "";
    public string? EcoUserId { get; set; }
    public string? EcoUserName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? ConfirmedServerId { get; set; }
    public Guid? ConfirmedUserServerId { get; set; }
}

// Jeton d'API du mod. Serveur : UserServerId nul, droits sur le serveur (upload des données, prix du marché).
// Joueur : lié à un UserServer, droits sur ses prix. Seul le hash SHA-256 est stocké.
public class EcoApiToken
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = "";
    public EcoLinkKind Kind { get; set; }
    [ForeignKey("Server")] public Guid ServerId { get; set; }
    [ForeignKey("UserServer")] public Guid? UserServerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public Server Server { get; set; }
    public UserServer? UserServer { get; set; }
}

// Prix moyens observés sur le serveur Eco, envoyés périodiquement par le mod (une ligne par objet).
// Sell = offres de vente des boutiques, Buy = offres d'achat, Traded = ventes réellement conclues sur la fenêtre.
public class ServerMarketPrice
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("Server")] public Guid ServerId { get; set; }
    [ForeignKey("ItemOrTag")] public Guid ItemOrTagId { get; set; }
    public decimal? SellAverage { get; set; }
    public decimal SellQuantity { get; set; }
    public int SellShops { get; set; }
    public decimal? BuyAverage { get; set; }
    public decimal BuyQuantity { get; set; }
    public int BuyShops { get; set; }
    public decimal? TradedAverage { get; set; }
    public decimal TradedQuantity { get; set; }

    public Server Server { get; set; }
    public ItemOrTag ItemOrTag { get; set; }

    // Ce que paierait un acheteur : le prix réellement pratiqué d'abord, puis ce que demandent les boutiques.
    public decimal? Suggestion => TradedAverage ?? SellAverage ?? BuyAverage;
}

// Connexion externe (Discord) rattachée à un compte. Optionnelle : le compte anonyme du navigateur reste valable.
public class UserLogin
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [ForeignKey("User")] public Guid UserId { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarHash { get; set; } // Discord avatar; null when the account uses Discord's default one.
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; }
}
