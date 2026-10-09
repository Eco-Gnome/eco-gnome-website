using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserShoppingListItemDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserShoppingListItem>
{
	public async Task<List<UserShoppingListItem>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserShoppingListItem>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserShoppingListItems
			.ToListAsync();
	}

	public async Task<List<UserShoppingListItem>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserShoppingListItem>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserShoppingListItems
			.Where(usli => usli.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<UserShoppingListItem?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserShoppingListItem?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserShoppingListItems
			.FirstOrDefaultAsync(usli => usli.Id == id);
	}

	private UserShoppingListItem CloneForDb(UserShoppingListItem userShoppingListItem)
	{
		return new UserShoppingListItem
		{
			Id = userShoppingListItem.Id,
			DataContextId = userShoppingListItem.DataContext.Id,
			ItemOrTagId = userShoppingListItem.ItemOrTag.Id,
			ChosenItemId = userShoppingListItem.ChosenItem?.Id,
			Stock = userShoppingListItem.Stock,
		};
	}

	public void Create(EcoCraftDbContext context, UserShoppingListItem userShoppingListItem)
	{
		context.Add(CloneForDb(userShoppingListItem));
	}

	public void Update(EcoCraftDbContext context, UserShoppingListItem userShoppingListItem)
	{
		var stub = new UserShoppingListItem
		{
			Id = userShoppingListItem.Id,
			Stock = userShoppingListItem.Stock,
			ChosenItemId = userShoppingListItem.ChosenItem?.Id,
		};
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Stock).IsModified = true;
		entry.Property(x => x.ChosenItemId).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserShoppingListItem userShoppingListItem)
	{
		context.QueueDelete<UserShoppingListItem>(userShoppingListItem.Id);
	}
}
