using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserCraftingTableDbService : IGenericDbService<UserCraftingTable>
	{
		private readonly EcoCraftDbContext _context;
		private readonly PluginModuleDbService _pluginModuleDbService;

		public UserCraftingTableDbService(PluginModuleDbService pluginModuleDbService, EcoCraftDbContext context)
		{
			_pluginModuleDbService = pluginModuleDbService;
			_context = context;
		}

		public async Task<List<UserCraftingTable>> GetAllAsync()
		{
			return await _context.UserCraftingTables.Include(uct => uct.UserServer)
													 .Include(uct => uct.CraftingTable)
													 .Include(uct => uct.PluginModule)
													 .ToListAsync();
		}

		public Task<List<UserCraftingTable>> GetByUserServerAsync(UserServer userServer)
		{
			return _context.UserCraftingTables
				.Where(s => s.UserServerId == userServer.Id)
				.ToListAsync();
		}

		public async Task<UserCraftingTable> GetByIdAsync(Guid id)
		{
			return await _context.UserCraftingTables.Include(uct => uct.UserServer)
													 .Include(uct => uct.CraftingTable)
													 .Include(uct => uct.PluginModule)
													 .FirstOrDefaultAsync(uct => uct.Id == id);
		}

		public async Task AddAsync(UserCraftingTable userCraftingTable)
		{
			await _context.UserCraftingTables.AddAsync(userCraftingTable);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserCraftingTable userCraftingTable)
		{
			_context.UserCraftingTables.Update(userCraftingTable);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userCraftingTable = await GetByIdAsync(id);
			if (userCraftingTable != null)
			{
				_context.UserCraftingTables.Remove(userCraftingTable);
				await _context.SaveChangesAsync();
			}
		}

		

		// Méthode pour récupérer les CraftingTables associées à un utilisateur
		public async Task<List<UserCraftingTable>> GetUserCraftingTablesByUserAsync(UserServer userServer)
		{
			return await _context.UserCraftingTables
				.Include(uct => uct.CraftingTable)
				.Include(uct => uct.PluginModule)
				.Where(uct => uct.UserServerId == userServer.Id)
				.ToListAsync();
		}
	}

}
