using ecocraft.Components;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using ecocraft.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddDbContext<EcoCraftDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserSettingService>();
builder.Services.AddScoped<ServerService>();
builder.Services.AddScoped<UserCraftingTableService>();
builder.Services.AddScoped<CraftingTableService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<PluginModuleService>();
builder.Services.AddScoped<SkillService>();
builder.Services.AddScoped<UserSkillService>();
builder.Services.AddScoped<UserElementService>();
builder.Services.AddScoped<ElementService>();
builder.Services.AddScoped<ItemOrTagService>();
builder.Services.AddScoped<UserPriceService>();

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
