using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserPriceDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserPrice>
{
	public async Task<List<UserPrice>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserPrice>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserPrices
			.ToListAsync();
	}

	public async Task<List<UserPrice>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserPrice>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserPrices
			.Where(up => up.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<List<UserPrice>> GetByDataContextForEcoApiAsync(DataContext dataContext, bool excludeNullPrices = false)
	{
		await using var context = await factory.CreateDbContextAsync();

		return await context.UserPrices
			.Where(up => up.DataContextId == dataContext.Id && (!excludeNullPrices || up.Price != null))
			.Include(up => up.ItemOrTag)
			.ThenInclude(i => i.AssociatedItems)
			.Include(up => up.UserMargin)
			.ToListAsync();
	}

	public async Task<UserPrice?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserPrice?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserPrices
			.FirstOrDefaultAsync(up => up.Id == id);
	}

	private UserPrice CloneForDb(UserPrice userPrice)
	{
		return new UserPrice
		{
			Id = userPrice.Id,
			ItemOrTagId = userPrice.ItemOrTag.Id,
			Price = userPrice.Price,
			MarginPrice = userPrice.MarginPrice,
			PrimaryUserElementId = userPrice.PrimaryUserElement?.Id,
			PrimaryUserPriceId = userPrice.PrimaryUserPrice?.Id,
			DataContextId = userPrice.DataContext.Id,
			OverrideIsBought = userPrice.OverrideIsBought,
			UserMarginId = userPrice.UserMargin?.Id,
		};
	}

	public void Create(EcoCraftDbContext context, UserPrice userPrice)
	{
		context.Add(CloneForDb(userPrice));
	}

	public void UpdateAll(EcoCraftDbContext context, UserPrice userPrice)
	{
		context.Attach(CloneForDb(userPrice)).State = EntityState.Modified;
	}

	public void UpdatePrice(EcoCraftDbContext context, UserPrice userPrice)
	{
		var stub = new UserPrice { Id = userPrice.Id, Price = userPrice.Price };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Price).IsModified = true;
	}

	public void UpdateOverrideIsBought(EcoCraftDbContext context, UserPrice userPrice)
	{
		var stub = new UserPrice { Id = userPrice.Id, OverrideIsBought = userPrice.OverrideIsBought };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.OverrideIsBought).IsModified = true;
	}

	public void UpdateUserMargin(EcoCraftDbContext context, UserPrice userPrice)
	{
		var stub = new UserPrice { Id = userPrice.Id, UserMarginId = userPrice.UserMarginId };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.UserMarginId).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserPrice userPrice)
	{
		var entity = new UserPrice { Id = userPrice.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
