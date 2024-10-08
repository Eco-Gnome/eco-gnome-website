using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class CraftingTableDbService(EcoCraftDbContext context) : IGenericNamedDbService<CraftingTable>
{
	public Task<List<CraftingTable>> GetAllAsync()
	{
		return context.CraftingTables
			.ToListAsync();
	}

	public Task<List<CraftingTable>> GetByServerAsync(Server server)
	{
		return context.CraftingTables
			.Where(ct => ct.ServerId == server.Id)
			.Include(ct => ct.PluginModules)
			.ToListAsync();
	}

	public Task<CraftingTable?> GetByIdAsync(Guid id)
	{
		return context.CraftingTables
			.FirstOrDefaultAsync(ct => ct.Id == id);
	}
	
	public Task<CraftingTable?> GetByNameAsync(string name)
	{
		return context.CraftingTables
			.FirstOrDefaultAsync(ct => ct.Name == name);
	}

	public CraftingTable Add(CraftingTable craftingTable)
	{
		context.CraftingTables.Add(craftingTable);

		return craftingTable;
	}

	public void Update(CraftingTable craftingTable)
	{
		context.CraftingTables.Update(craftingTable);
	}

	public void Delete(CraftingTable craftingTable)
	{
		context.CraftingTables.Remove(craftingTable);
	}
}