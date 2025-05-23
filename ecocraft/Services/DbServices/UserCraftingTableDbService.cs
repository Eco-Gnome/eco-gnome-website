﻿using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserCraftingTableDbService(EcoCraftDbContext context)
	: IGenericUserDbService<UserCraftingTable>
{
	public Task<List<UserCraftingTable>> GetAllAsync()
	{
		return context.UserCraftingTables
			.Include(uct => uct.CraftingTable)
			.Include(uct => uct.PluginModule)
			.Include(uct => uct.SkilledPluginModules)
			.ToListAsync();
	}

	public Task<List<UserCraftingTable>> GetByDataContextAsync(DataContext dataContext)
	{
		return context.UserCraftingTables
			.Where(s => s.DataContextId == dataContext.Id)
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
