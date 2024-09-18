using ecocraft.Components;
using ecocraft.Models;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using ecocraft.Data;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddDbContext<EcoCraftDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<CraftingTableService>();
builder.Services.AddScoped<UserCraftingTableService>();
builder.Services.AddScoped<SkillService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserSkillService>();
builder.Services.AddScoped<UserInputPriceService>();


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
