using ecocraft.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class CraftingTablePluginModuleService : IGenericService<CraftingTablePluginModule>
	{
		private readonly EcoCraftDbContext _context;

		public CraftingTablePluginModuleService(EcoCraftDbContext context)
		{
			_context = context;
		}

		// Get all CraftingTablePluginModules
		public async Task<List<CraftingTablePluginModule>> GetAllAsync()
		{
			return await _context.CraftingTablePluginModules
								 .Include(ctpm => ctpm.CraftingTable)
								 .Include(ctpm => ctpm.PluginModule)
								 .Include(ctpm => ctpm.Server)
								 .ToListAsync();
		}

		// Get a CraftingTablePluginModule by Id
		public async Task<CraftingTablePluginModule> GetByIdAsync(Guid id)
		{
			return await _context.CraftingTablePluginModules
								 .Include(ctpm => ctpm.CraftingTable)
								 .Include(ctpm => ctpm.PluginModule)
								 .Include(ctpm => ctpm.Server)
								 .FirstOrDefaultAsync(ctpm => ctpm.Id == id);
		}

		// Create a new CraftingTablePluginModule
		public async Task CreateAsync(CraftingTablePluginModule craftingTablePluginModule)
		{
			_context.CraftingTablePluginModules.Add(craftingTablePluginModule);
			await _context.SaveChangesAsync();
		}

		// Update an existing CraftingTablePluginModule
		public async Task UpdateAsync(CraftingTablePluginModule craftingTablePluginModule)
		{
			_context.CraftingTablePluginModules.Update(craftingTablePluginModule);
			await _context.SaveChangesAsync();
		}

		// Delete a CraftingTablePluginModule by Id
		public async Task DeleteAsync(Guid id)
		{
			var craftingTablePluginModule = await _context.CraftingTablePluginModules.FindAsync(id);
			if (craftingTablePluginModule != null)
			{
				_context.CraftingTablePluginModules.Remove(craftingTablePluginModule);
				await _context.SaveChangesAsync();
			}
		}

		public async Task AddAsync(CraftingTablePluginModule CraftingTablePluginModule)
		{
			await _context.CraftingTablePluginModules.AddAsync(CraftingTablePluginModule);
			await _context.SaveChangesAsync();
		}
	}
}
