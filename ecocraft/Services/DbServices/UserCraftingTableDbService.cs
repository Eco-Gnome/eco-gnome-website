using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserCraftingTableDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserCraftingTable>
{
	public async Task<List<UserCraftingTable>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserCraftingTable>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserCraftingTables
			.Include(uct => uct.CraftingTable)
			.Include(uct => uct.PluginModule)
			.Include(uct => uct.SkilledPluginModules)
			.ToListAsync();
	}

	public async Task<List<UserCraftingTable>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserCraftingTable>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserCraftingTables
			.Where(s => s.DataContextId == dataContext.Id)
			.Include(uct => uct.CraftingTable)
			.Include(uct => uct.PluginModule)
			.Include(uct => uct.SkilledPluginModules)
			.ToListAsync();
	}

	public async Task<UserCraftingTable?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserCraftingTable?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserCraftingTables
			.FirstOrDefaultAsync(uct => uct.Id == id);
	}

	private UserCraftingTable CloneForDb(UserCraftingTable userCraftingTable)
	{
		return new UserCraftingTable
		{
			Id = userCraftingTable.Id,
			DataContextId = userCraftingTable.DataContext.Id,
			CraftingTableId = userCraftingTable.CraftingTable.Id,
			PluginModuleId = userCraftingTable.PluginModule?.Id,
			CraftMinuteFee = userCraftingTable.CraftMinuteFee,
		};
	}

	public void Create(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		context.Add(CloneForDb(userCraftingTable));
	}

	public void UpdateAll(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		context.Attach(CloneForDb(userCraftingTable)).State = EntityState.Modified;
	}

	public async Task UpdateAllAsync(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		var existing = await context.UserCraftingTables
			.Include(uct => uct.SkilledPluginModules)
			.FirstAsync(uct => uct.Id == userCraftingTable.Id);

		existing.PluginModuleId = userCraftingTable.PluginModule?.Id;
		existing.CraftMinuteFee = userCraftingTable.CraftMinuteFee;

		// Delta-based update: Clear()+ReAdd on a tracked skip-nav collection leaves the
		// just-cleared join entries in Deleted state; re-adding the same tracked instance
		// does not revert it, so the previously persisted modules silently get DELETEd
		// while only newly-attached stubs INSERT.
		var desiredIds = userCraftingTable.SkilledPluginModules.Select(pm => pm.Id).ToHashSet();

		foreach (var pm in existing.SkilledPluginModules.Where(pm => !desiredIds.Contains(pm.Id)).ToList())
		{
			existing.SkilledPluginModules.Remove(pm);
		}

		var existingIds = existing.SkilledPluginModules.Select(pm => pm.Id).ToHashSet();
		foreach (var pmId in desiredIds.Where(id => !existingIds.Contains(id)))
		{
			existing.SkilledPluginModules.Add(GetTrackedPluginModule(context, pmId));
		}
	}

	private static PluginModule GetTrackedPluginModule(EcoCraftDbContext context, Guid pluginModuleId)
	{
		var trackedEntry = context.ChangeTracker.Entries<PluginModule>()
			.FirstOrDefault(entry => entry.Entity.Id == pluginModuleId);
		if (trackedEntry is not null)
		{
			return trackedEntry.Entity;
		}

		return context.PluginModules.Attach(new PluginModule { Id = pluginModuleId }).Entity;
	}

	public void UpdateCraftMinuteFee(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		var stub = new UserCraftingTable { Id = userCraftingTable.Id, CraftMinuteFee = userCraftingTable.CraftMinuteFee };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.CraftMinuteFee).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		context.QueueDelete<UserCraftingTable>(userCraftingTable.Id);
	}
}
