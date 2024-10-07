using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserCraftingTableDbService(EcoCraftDbContext context)
		: IGenericUserDbService<UserCraftingTable>
	{
		public Task<List<UserCraftingTable>> GetAllAsync()
		{
			return context.UserCraftingTables.Include(uct => uct.UserServer)
													 .Include(uct => uct.CraftingTable)
													 .Include(uct => uct.PluginModule)
													 .ToListAsync();
		}

		public Task<List<UserCraftingTable>> GetByUserServerAsync(UserServer userServer)
		{
			return context.UserCraftingTables
				.Where(s => s.UserServerId == userServer.Id)
                .ToListAsync();
		}

		public Task<UserCraftingTable?> GetByIdAsync(Guid id)
		{
			return context.UserCraftingTables
				.FirstOrDefaultAsync(uct => uct.Id == id);
		}

		public UserCraftingTable Add(UserCraftingTable userCraftingTable)
		{
			context.UserCraftingTables.Add(userCraftingTable);
			
			return userCraftingTable;
		}

		public void Update(UserCraftingTable userCraftingTable)
		{
			context.UserCraftingTables.Update(userCraftingTable);
		}

		public void Delete(UserCraftingTable userCraftingTable)
		{
			context.UserCraftingTables.Remove(userCraftingTable);
		}
	}

}
