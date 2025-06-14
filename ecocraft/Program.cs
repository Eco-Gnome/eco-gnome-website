using System.Globalization;
using ecocraft.Components;
using ecocraft.Extensions;
using ecocraft.Models;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ecocraft.Services;
using ecocraft.Services.DbServices;
using ecocraft.Services.ImportData;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();

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

builder.Services.AddDbContext<EcoCraftDbContext>(options =>
    options
        .UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"),
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
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<ShoppingListDataService>();

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

// Appliquer automatiquement les migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EcoCraftDbContext>();
    dbContext.Database.Migrate(); // Applique toutes les migrations en attente
}

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
