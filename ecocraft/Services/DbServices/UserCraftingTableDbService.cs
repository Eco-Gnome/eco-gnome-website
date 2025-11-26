using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserCraftingTableDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserCraftingTable>
{
	public async Task<List<UserCraftingTable>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.UserCraftingTables
			.Include(uct => uct.CraftingTable)
			.Include(uct => uct.PluginModule)
			.Include(uct => uct.SkilledPluginModules)
			.ToListAsync();
	}

	public async Task<List<UserCraftingTable>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.UserCraftingTables
			.Where(s => s.DataContextId == dataContext.Id)
			.Include(uct => uct.CraftingTable)
			.Include(uct => uct.PluginModule)
			.Include(uct => uct.SkilledPluginModules)
			.ToListAsync();
	}

	public async Task<UserCraftingTable?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

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

	public void UpdateCraftMinuteFee(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		var entry = context.Attach(userCraftingTable);
		entry.Property(x => x.CraftMinuteFee).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserCraftingTable userCraftingTable)
	{
		var entity = new UserCraftingTable { Id = userCraftingTable.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
