using Microsoft.EntityFrameworkCore;
using ecocraft.Models;

namespace ecocraft.Services
{
    public class PluginModuleService : IGenericService<PluginModule>
    {
        private readonly EcoCraftDbContext _context;

        public PluginModuleService(EcoCraftDbContext context)
        {
            _context = context;
        }

        public async Task<List<PluginModule>> GetAllAsync()
        {
            return await _context.PluginModules
                .Include(pm => pm.CraftingTables)
                .Include(pm => pm.Server)
                .ToListAsync();
        }

        public async Task<PluginModule> GetByIdAsync(Guid id)
        {
            return await _context.PluginModules
                .Include(pm => pm.CraftingTables)
                .Include(pm => pm.Server)
                .FirstOrDefaultAsync(pm => pm.Id == id);
        }

        public async Task<PluginModule> GetByNameAsync(string name)
        {
            return await _context.PluginModules
                .Include(pm => pm.CraftingTables)
                .Include(pm => pm.Server)
                .FirstOrDefaultAsync(pm => pm.Name == name);
        }

        public async Task AddAsync(PluginModule pluginModule)
        {
            await _context.PluginModules.AddAsync(pluginModule);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(PluginModule pluginModule)
        {
            _context.PluginModules.Update(pluginModule);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var pluginModule = await GetByIdAsync(id);
            if (pluginModule != null)
            {
                _context.PluginModules.Remove(pluginModule);
                await _context.SaveChangesAsync();
            }
        }
    }
}