using System.Globalization;
using ecocraft.Components;
using ecocraft.Extensions;
using ecocraft.Models;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using ecocraft.Services;
using ecocraft.Services.DbServices;
using ecocraft.Services.ImportData;
using Microsoft.AspNetCore.Components.Server;
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

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options => { options.DetailedErrors = true; });


builder.Services.Configure<CircuitOptions>(options =>
{
    options.DetailedErrors = true;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddDbContext<EcoCraftDbContext>(options =>
    options
        .UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
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
builder.Services.AddScoped<UserCraftingTableDbService>();
builder.Services.AddScoped<UserDbService>();
builder.Services.AddScoped<UserElementDbService>();
builder.Services.AddScoped<UserPriceDbService>();
builder.Services.AddScoped<UserRecipeDbService>();
builder.Services.AddScoped<UserMarginDbService>();
builder.Services.AddScoped<UserSettingDbService>();
builder.Services.AddScoped<UserSkillDbService>();

// Business Services
builder.Services.AddScoped<ContextService>();
builder.Services.AddScoped<ImportDataService>();
builder.Services.AddScoped<PriceCalculatorService>();
builder.Services.AddScoped<ServerDataService>();
builder.Services.AddScoped<UserServerDataService>();

// Util Services
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<JSInteropService>();

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
app.UseRequestLocalization(locOptions.Value);

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

app.UseStaticFiles();
app.MapControllers();
app.UseAntiforgery();

//app.UseAuthentication();.
//app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

StaticEnvironmentAccessor.WebHostEnvironment = app.Services.GetRequiredService<IWebHostEnvironment>();

app.Run();
