using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class PluginModuleDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericNamedDbService<PluginModule>
{
    public async Task<List<PluginModule>> GetAllAsync(EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.PluginModules
            .Include(s => s.LocalizedName)
            .Include(s => s.Skill)
            .ToListAsync();
    }

    public async Task<List<PluginModule>> GetByServerAsync(Server server, EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.PluginModules
            .Where(s => s.ServerId == server.Id)
            .Include(s => s.LocalizedName)
            .Include(s => s.Skill)
            .ToListAsync();
    }

    public async Task<PluginModule?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.PluginModules
            .FirstOrDefaultAsync(pm => pm.Id == id);
    }

    public async Task<PluginModule?> GetByNameAsync(string name, EcoCraftDbContext? context = null)
    {
        context ??= await factory.CreateDbContextAsync();

        return await context.PluginModules
            .FirstOrDefaultAsync(pm => pm.Name == name);
    }

    private PluginModule CloneForDb(PluginModule pluginModule)
    {
        return new PluginModule
        {
            Id = pluginModule.Id,
            Name = pluginModule.Name,
            LocalizedNameId = pluginModule.LocalizedName.Id,
            PluginType = pluginModule.PluginType,
            Percent = pluginModule.Percent,
            SkillPercent = pluginModule.SkillPercent,
            SkillId = pluginModule.Skill?.Id,
            ServerId = pluginModule.Server.Id,
        };
    }

    public void Create(EcoCraftDbContext context, PluginModule pluginModule)
    {
        context.Add(CloneForDb(pluginModule));
    }

    public void UpdateAll(EcoCraftDbContext context, PluginModule pluginModule)
    {
        context.Attach(CloneForDb(pluginModule)).State = EntityState.Modified;
    }

    public void Destroy(EcoCraftDbContext context, PluginModule pluginModule)
    {
        var entity = new PluginModule { Id = pluginModule.Id };
        context.Entry(entity).State = EntityState.Deleted;
    }
}
