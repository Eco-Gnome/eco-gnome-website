using ecocraft.Components;
using ecocraft.Models;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using ecocraft.Services;
using ecocraft.Services.ImportData;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddDbContext<EcoCraftDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.AddScoped<UserSettingDbService>();
builder.Services.AddScoped<UserSkillDbService>();

// Business Services
builder.Services.AddScoped<ContextService>();
builder.Services.AddScoped<ImportDataService>();
builder.Services.AddScoped<PriceCalculatorService>();
builder.Services.AddScoped<ServerDataService>();
builder.Services.AddScoped<UserDataService>();

// Util Services
builder.Services.AddScoped<LocalStorageService>();

// Authorization
builder.Services.AddScoped<Authorization>();
builder.Services.AddAuthorization(config =>
{
    config.AddPolicy("IsServerAdmin", policy =>
        policy.Requirements.Add(new IsServerAdminRequirement()));
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
