using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserCraftingTableService : IGenericService<UserCraftingTable>
	{
		private readonly EcoCraftDbContext _context;

		public UserCraftingTableService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<UserCraftingTable>> GetAllAsync()
		{
			return await _context.UserCraftingTables.Include(uct => uct.User)
													 .Include(uct => uct.CraftingTable)
													 .Include(uct => uct.PluginModule)
													 .Include(uct => uct.Server)
													 .ToListAsync();
		}

		public async Task<UserCraftingTable> GetByIdAsync(Guid id)
		{
			return await _context.UserCraftingTables.Include(uct => uct.User)
													 .Include(uct => uct.CraftingTable)
													 .Include(uct => uct.PluginModule)
													 .Include(uct => uct.Server)
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
	}

}
