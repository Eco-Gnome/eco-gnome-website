using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class CraftingTableDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericNamedDbService<CraftingTable>
{
	public async Task<List<CraftingTable>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<CraftingTable>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.CraftingTables
			.ToListAsync();
	}

	public async Task<List<CraftingTable>> GetByServerAsync(Server server)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByServerAsync(server, context);
	}

	public async Task<List<CraftingTable>> GetByServerAsync(Server server, EcoCraftDbContext context)
	{
		return await context.CraftingTables
			.Where(ct => ct.ServerId == server.Id)
			.Include(ct => ct.PluginModules)
			.Include(s => s.LocalizedName)
			.ToListAsync();
	}

	public async Task<CraftingTable?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<CraftingTable?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.CraftingTables
			.FirstOrDefaultAsync(ct => ct.Id == id);
	}

	public async Task<CraftingTable?> GetByNameAsync(string name)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByNameAsync(name, context);
	}

	public async Task<CraftingTable?> GetByNameAsync(string name, EcoCraftDbContext context)
	{
		return await context.CraftingTables
			.FirstOrDefaultAsync(ct => ct.Name == name);
	}

	private CraftingTable CloneForDb(CraftingTable craftingTable)
	{
		return new CraftingTable
		{
			Id = craftingTable.Id,
			Name = craftingTable.Name,
			LocalizedNameId = craftingTable.LocalizedName.Id,
			ServerId = craftingTable.Server.Id,
		};
	}

	public void Create(EcoCraftDbContext context, CraftingTable craftingTable)
	{
		context.Add(CloneForDb(craftingTable));
	}

	public void UpdateAll(EcoCraftDbContext context, CraftingTable craftingTable)
	{
		context.Attach(CloneForDb(craftingTable)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, CraftingTable craftingTable)
	{
		var entity = new CraftingTable { Id = craftingTable.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
