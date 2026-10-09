using System.Globalization;
using ecocraft.Components;
using ecocraft.Extensions;
using ecocraft.Models;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using ecocraft.Services;
using ecocraft.Services.DbServices;
using ecocraft.Services.ImportData;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Threading.RateLimiting;
using ecocraft.Controllers;
using ecocraft.Services.EcoLink;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
                             ?? Path.Combine(builder.Environment.ContentRootPath, ".aspnet-dp-keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .SetApplicationName("ecocraft")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// var supportedCultures = new[] { "en", "en-US", "en-GB", "fr", "fr-FR", "es-ES", "es", "de", "de-DE" }; // Ajoutez les cultures que vous supportez
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
    options.SupportedUICultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Chaque circuit porte sa copie du graphe serveur (~130 Mo observés en prod). Par défaut Blazor
        // garde un circuit déconnecté 3 min en attendant une reconnexion ; 1 min suffit pour un
        // rechargement d'onglet et libère la RAM des onglets perdus trois fois plus vite.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);
    })
    // Le planificateur de bâtiment renvoie le plan complet (JSON) à chaque commit d'édition ; la limite
    // par défaut de Blazor Server (32 Ko) est trop basse pour un plan de 200×200.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 2 * 1024 * 1024);

builder.Services.AddMudServices();
builder.Services.AddMudMarkdownServices();

builder.Services.AddDbContextFactory<EcoCraftDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .EnableSensitiveDataLogging()
        .UseLoggerFactory(LoggerFactory.Create(bd =>
        {
            bd
                .AddConsole()
                .AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Error);
        }))
    );

builder.Services.AddControllers();

// DB Services
builder.Services.AddScoped<CraftingTableDbService>();
builder.Services.AddScoped<ElementDbService>();
builder.Services.AddScoped<ItemOrTagDbService>();
builder.Services.AddScoped<PluginModuleDbService>();
builder.Services.AddScoped<RecipeDbService>();
builder.Services.AddScoped<ServerDbService>();
builder.Services.AddScoped<SkillDbService>();
builder.Services.AddScoped<TalentDbService>();
builder.Services.AddScoped<DynamicValueDbService>();
builder.Services.AddScoped<ModifierDbService>();
builder.Services.AddScoped<UserCraftingTableDbService>();
builder.Services.AddScoped<UserDbService>();
builder.Services.AddScoped<UserElementDbService>();
builder.Services.AddScoped<UserPriceDbService>();
builder.Services.AddScoped<UserRecipeDbService>();
builder.Services.AddScoped<UserMarginDbService>();
builder.Services.AddScoped<UserSettingDbService>();
builder.Services.AddScoped<UserServerDbService>();
builder.Services.AddScoped<UserTalentDbService>();
builder.Services.AddScoped<UserSkillDbService>();
builder.Services.AddScoped<UserAutomationInputDbService>();
builder.Services.AddScoped<UserAutomationTargetDbService>();
builder.Services.AddScoped<DataContextDbService>();
builder.Services.AddScoped<ModUploadHistoryDbService>();
builder.Services.AddScoped<BuildingPlanDbService>();

// Business Services
builder.Services.AddScoped<ContextService>();
builder.Services.AddScoped<ImportDataService>();
builder.Services.AddScoped<PriceCalculatorService>();
builder.Services.AddScoped<ServerDataService>();
builder.Services.AddScoped<ServerDataEditorService>();
builder.Services.AddScoped<UserServerDataService>();
builder.Services.AddScoped<CraftingTableFuelCostService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<ShoppingListDataService>();
builder.Services.AddScoped<ShoppingListGraphService>();
builder.Services.AddScoped<EconomyViewerService>();
builder.Services.AddScoped<EconomyViewerDisplayService>();
builder.Services.AddScoped<BuildingPlannerCatalogService>();
builder.Services.AddScoped<BuildingPlannerService>();
builder.Services.AddSingleton<BuildingPlannerPromptService>();   // sans état : clé du fournisseur IA par serveur, déchiffrée à l'appel
builder.Services.AddScoped<EcoLinkService>();
builder.Services.AddScoped<EcoApiDataService>();
builder.Services.AddScoped<EcoMarketService>();

// Requêtes vers les serveurs web Eco (prix du marché). Adresses privées refusées, sauf en dev avec un serveur Eco local.
var allowPrivateEcoAddresses = builder.Configuration.GetValue<bool>("EcoMarket:AllowPrivateAddresses");
builder.Services.AddHttpClient(EcoMarketService.HttpClientName)
    .ConfigureHttpClient(client => client.MaxResponseContentBufferSize = 5_000_000) // A market answer weighs a few hundred KB: anything far bigger is refused.
    .ConfigurePrimaryHttpMessageHandler(() => allowPrivateEcoAddresses
        ? new SocketsHttpHandler()
        : new SocketsHttpHandler { ConnectCallback = EcoMarketService.ConnectPublicOnlyAsync });
builder.Services.AddScoped<UserLoginService>();

// Util Services
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<LiveCircuitTracker>();
builder.Services.AddScoped<CircuitSession>();
builder.Services.AddScoped<CircuitHandler, LiveCircuitHandler>();
builder.Services.AddScoped<LocalizationService>();

// Authorization
builder.Services.AddScoped<Authorization>();
builder.Services.AddAuthorization(config =>
{
    config.AddPolicy("IsServerAdmin", policy =>
        policy.Requirements.Add(new IsServerAdminRequirement()));
});

// Connexion Discord, optionnelle : le compte anonyme du navigateur reste l'identité de base. Sans Discord:ClientId
// configuré, le bouton n'apparaît pas et seul le cookie (vide) est enregistré.
var authentication = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ecognome.auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(180);
        options.SlidingExpiration = true;
    });

if (DiscordAuth.IsConfigured(builder.Configuration))
{
    authentication.AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"]!;
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
        options.CallbackPath = "/auth/discord/callback";
        options.SaveTokens = false;
        // The return from Discord is a top-level navigation: Lax is enough, and over plain http (local dev) browsers
        // refuse the default SameSite=None cookie without Secure, which fails the login with "Correlation failed".
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    });
}

builder.Services.AddCascadingAuthenticationState();

// Derrière le reverse proxy : schéma https pour l'URL de retour OAuth, IP réelle pour la limite de débit.
// Only the reverse proxy is trusted: it reaches the container from the host (loopback or a Docker bridge), so an address
// a client writes in X-Forwarded-For itself is ignored.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var network in new[] { "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "::1/128", "fc00::/7" })
        options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(network));
});

// Le mod envoie le JSON des données du serveur compressé en gzip.
builder.Services.AddRequestDecompression();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(EcoLinkRateLimit.Policy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseForwardedHeaders();

var locOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions!.Value);

await ApplyMigrationsWithRetryAsync(app.Services, app.Logger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;

    if (path != null && path.StartsWith("/assets/eco-icons/"))
    {
        var serverId = context.Request.Query.TryGetValue("serverId", out var sid) ? sid.ToString() : null;
        var filePathWithServer = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets",
            serverId ?? "no_found", path.Substring("/assets/eco-icons/".Length));
        var filePathWithServerAndFixupForTags = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets",
            serverId ?? "no_found", path.Substring("/assets/eco-icons/".Length).Replace(".png", "Item.png"));

        if (serverId is not null && File.Exists(filePathWithServer))
        {
            context.Request.Path = path.Replace("eco-icons", serverId);
        }
        else if (serverId is not null && File.Exists(filePathWithServerAndFixupForTags))
        {
            context.Request.Path = path.Replace("eco-icons", serverId).Replace(".png", "Item.png");
        }
        else
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "eco-icons",
                path.Substring("/assets/eco-icons/".Length));
            if (!File.Exists(filePath))
            {
                var filePathWithFixupTags = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets",
                    "eco-icons", path.Substring("/assets/eco-icons/".Length).Replace(".png", "Item.png"));
                if (File.Exists(filePathWithFixupTags))
                {
                    context.Request.Path = path.Replace("eco-icons", "mod-icons").Replace(".png", "Item.png");
                }
                else
                {
                    context.Request.Path = path.Replace("eco-icons", "mod-icons");
                }
            }
        }
    }

    await next();
});

// Bundles de formes 3D : ré-extraits sans changer de nom, le navigateur doit les revalider (ETag) à chaque chargement.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/assets/forms"))
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});
app.UseRequestDecompression();
app.UseAuthentication();
app.UseRateLimiter();
app.MapControllers();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

StaticEnvironmentAccessor.WebHostEnvironment = app.Services.GetRequiredService<IWebHostEnvironment>();

app.Run();

static async Task ApplyMigrationsWithRetryAsync(IServiceProvider services, ILogger logger)
{
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EcoCraftDbContext>>();
            await using var dbContext = await factory.CreateDbContextAsync();

            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
            return;
        }
        catch (Exception ex) when (IsTransientDatabaseStartupFailure(ex) && attempt < maxAttempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 15));
            logger.LogWarning(ex,
                "Database is not ready yet while applying migrations (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s.",
                attempt,
                maxAttempts,
                delay.TotalSeconds);

            await Task.Delay(delay);
        }
    }

    await using var finalScope = services.CreateAsyncScope();
    var finalFactory = finalScope.ServiceProvider.GetRequiredService<IDbContextFactory<EcoCraftDbContext>>();
    await using var finalContext = await finalFactory.CreateDbContextAsync();
    await finalContext.Database.MigrateAsync();
}

static bool IsTransientDatabaseStartupFailure(Exception exception)
{
    return exception switch
    {
        PostgresException { SqlState: "57P03" } => true,
        NpgsqlException { InnerException: not null } npgsqlException => IsTransientDatabaseStartupFailure(
            npgsqlException.InnerException),
        AggregateException aggregateException => aggregateException.InnerExceptions.Any(innerException =>
            IsTransientDatabaseStartupFailure(innerException)),
        _ => false,
    };
}
