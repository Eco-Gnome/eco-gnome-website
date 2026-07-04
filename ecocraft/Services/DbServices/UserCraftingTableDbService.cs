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
			.Include(uct => uct.FuelItem)
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
			.Include(uct => uct.FuelItem)
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
			FuelItemId = userCraftingTable.FuelItem?.Id,
			AdditionalCraftMinuteFee = userCraftingTable.AdditionalCraftMinuteFee,
			TotalCraftMinuteFee = userCraftingTable.TotalCraftMinuteFee,
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
		// A freshly Create()'d UCT is Added but not yet persisted: querying the DB with
		// FirstAsync would throw "Sequence contains no elements". In that case we operate
		// on the tracked Added instance instead (its SkilledPluginModules collection is
		// empty, so the delta logic below simply inserts every desired module).
		var addedEntity = context.ChangeTracker.Entries<UserCraftingTable>()
			.FirstOrDefault(e => e.State == EntityState.Added && e.Entity.Id == userCraftingTable.Id)
			?.Entity;

		var existing = addedEntity
			?? await context.UserCraftingTables
				.Include(uct => uct.SkilledPluginModules)
				.FirstAsync(uct => uct.Id == userCraftingTable.Id);

		existing.PluginModuleId = userCraftingTable.PluginModule?.Id;
		existing.FuelItemId = userCraftingTable.FuelItem?.Id;
		existing.AdditionalCraftMinuteFee = userCraftingTable.AdditionalCraftMinuteFee;
		existing.TotalCraftMinuteFee = userCraftingTable.TotalCraftMinuteFee;

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

	public void UpdateTotalCraftMinuteFee(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		var stub = new UserCraftingTable { Id = userCraftingTable.Id, TotalCraftMinuteFee = userCraftingTable.TotalCraftMinuteFee };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.TotalCraftMinuteFee).IsModified = true;
	}

	public void UpdateAdditionalCraftMinuteFee(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		var stub = new UserCraftingTable
		{
			Id = userCraftingTable.Id,
			AdditionalCraftMinuteFee = userCraftingTable.AdditionalCraftMinuteFee,
			TotalCraftMinuteFee = userCraftingTable.TotalCraftMinuteFee,
		};
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.AdditionalCraftMinuteFee).IsModified = true;
		entry.Property(x => x.TotalCraftMinuteFee).IsModified = true;
	}

	public void UpdateFuelItem(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		var stub = new UserCraftingTable
		{
			Id = userCraftingTable.Id,
			FuelItemId = userCraftingTable.FuelItemId,
			TotalCraftMinuteFee = userCraftingTable.TotalCraftMinuteFee,
		};
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.FuelItemId).IsModified = true;
		entry.Property(x => x.TotalCraftMinuteFee).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		context.QueueDelete<UserCraftingTable>(userCraftingTable.Id);
	}
}
