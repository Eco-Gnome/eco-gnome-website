using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class PluginModuleDbService(EcoCraftDbContext context) : IGenericNamedDbService<PluginModule>
{
    public Task<List<PluginModule>> GetAllAsync()
    {
        return context.PluginModules
            .Include(s => s.LocalizedName)
            .Include(s => s.Skill)
            .ToListAsync();
    }

    public Task<List<PluginModule>> GetByServerAsync(Server server)
    {
        return context.PluginModules
            .Where(s => s.ServerId == server.Id)
            .Include(s => s.LocalizedName)
            .Include(s => s.Skill)
            .ToListAsync();
    }

    public Task<PluginModule?> GetByIdAsync(Guid id)
    {
        return context.PluginModules
            .FirstOrDefaultAsync(pm => pm.Id == id);
    }

    public Task<PluginModule?> GetByNameAsync(string name)
    {
        return context.PluginModules
            .FirstOrDefaultAsync(pm => pm.Name == name);
    }

    public PluginModule Add(PluginModule talent)
    {
        context.PluginModules.Add(talent);

        return talent;
    }

    public void Update(PluginModule pluginModule)
    {
        context.PluginModules.Update(pluginModule);
    }

    public void Delete(PluginModule pluginModule)
    {
        context.PluginModules.Remove(pluginModule);
    }
}
