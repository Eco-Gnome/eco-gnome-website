using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserCraftingTableDbService(EcoCraftDbContext context)
	: IGenericUserDbService<UserCraftingTable>
{
	public Task<List<UserCraftingTable>> GetAllAsync()
	{
		return context.UserCraftingTables.Include(uct => uct.UserServer)
			.Include(uct => uct.CraftingTable)
			.Include(uct => uct.PluginModule)
			.Include(uct => uct.SkilledPluginModules)
			.ToListAsync();
	}

	public Task<List<UserCraftingTable>> GetByUserServerAsync(UserServer userServer)
	{
		return context.UserCraftingTables
			.Where(s => s.UserServerId == userServer.Id)
			.Include(uct => uct.CraftingTable)
			.Include(uct => uct.PluginModule)
			.Include(uct => uct.SkilledPluginModules)
			.ToListAsync();
	}

	public Task<UserCraftingTable?> GetByIdAsync(Guid id)
	{
		return context.UserCraftingTables
			.FirstOrDefaultAsync(uct => uct.Id == id);
	}

	public UserCraftingTable Add(UserCraftingTable talent)
	{
		context.UserCraftingTables.Add(talent);
		talent.CraftingTable.CurrentUserCraftingTable = talent;

		return talent;
	}

	public void Update(UserCraftingTable userCraftingTable)
	{
		context.UserCraftingTables.Update(userCraftingTable);
	}

	public void Delete(UserCraftingTable userCraftingTable)
	{
		userCraftingTable.CraftingTable.CurrentUserCraftingTable = null;

		context.UserCraftingTables.Remove(userCraftingTable);
	}
}
