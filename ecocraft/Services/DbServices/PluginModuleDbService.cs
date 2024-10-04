using Microsoft.EntityFrameworkCore;
using ecocraft.Models;

namespace ecocraft.Services;

public class PluginModuleDbService(EcoCraftDbContext context) : IGenericNamedDbService<PluginModule>
{
    public Task<List<PluginModule>> GetAllAsync()
    {
        return context.PluginModules
            .ToListAsync();
    }

    public Task<List<PluginModule>> GetByServerAsync(Server server)
    {
        return context.PluginModules
            .Where(s => s.ServerId == server.Id)
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

    public PluginModule Add(PluginModule pluginModule)
    {
        context.PluginModules.Add(pluginModule);

        return pluginModule;
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