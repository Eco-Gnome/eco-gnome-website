using System.Globalization;
using System.IO;
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
    .AddInteractiveServerComponents();

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
                .AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Warning);
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
builder.Services.AddScoped<DataContextDbService>();
builder.Services.AddScoped<ModUploadHistoryDbService>();

// Business Services
builder.Services.AddScoped<ContextService>();
builder.Services.AddScoped<ImportDataService>();
builder.Services.AddScoped<PriceCalculatorService>();
builder.Services.AddScoped<ServerDataService>();
builder.Services.AddScoped<UserServerDataService>();
builder.Services.AddScoped<VideoUploadService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<ShoppingListDataService>();
builder.Services.AddScoped<EconomyViewerService>();
builder.Services.AddScoped<EconomyViewerDisplayService>();

// Util Services
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<LocalizationService>();

// Authorization
builder.Services.AddScoped<Authorization>();
builder.Services.AddAuthorization(config =>
{
    config.AddPolicy("IsServerAdmin", policy =>
        policy.Requirements.Add(new IsServerAdminRequirement()));
});

// Authentication Configuration
/*builder.Services.AddAuthentication(options =>
    {
        // Configure your authentication scheme here
        // For example, using cookies or JWT tokens
        options.DefaultAuthenticateScheme = "YourAuthenticationScheme"; // Remplace par ton schéma
        options.DefaultChallengeScheme = "YourAuthenticationScheme"; // Remplace par ton schéma
    })
    .AddYourAuthenticationScheme(); // Remplace par ta méthode d'authentification (ex. .AddCookie(), .AddJwtBearer(), etc.)*/

var app = builder.Build();

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
        var filePathWithServer = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", serverId ?? "no_found", path.Substring("/assets/eco-icons/".Length));
        var filePathWithServerAndFixupForTags = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", serverId ?? "no_found", path.Substring("/assets/eco-icons/".Length).Replace(".png", "Item.png"));

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
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "eco-icons", path.Substring("/assets/eco-icons/".Length));
            if (!File.Exists(filePath))
            {
                var filePathWithFixupTags = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "eco-icons", path.Substring("/assets/eco-icons/".Length).Replace(".png", "Item.png"));
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

app.UseStaticFiles();
app.MapControllers();
app.UseAntiforgery();

//app.UseAuthentication();.
//app.UseAuthorization();

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
        NpgsqlException { InnerException: not null } npgsqlException => IsTransientDatabaseStartupFailure(npgsqlException.InnerException),
        AggregateException aggregateException => aggregateException.InnerExceptions.Any(innerException => IsTransientDatabaseStartupFailure(innerException)),
        _ => false,
    };
}
