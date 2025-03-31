using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserPriceDbService(EcoCraftDbContext context) : IGenericUserDbService<UserPrice>
{
	public Task<List<UserPrice>> GetAllAsync()
	{
		return context.UserPrices
			.ToListAsync();
	}

	public Task<List<UserPrice>> GetByDataContextAsync(DataContext dataContext)
	{
		return context.UserPrices
			.Where(up => up.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public Task<List<UserPrice>> GetByDataContextForEcoApiAsync(DataContext dataContext, bool excludeNullPrices = false)
	{
		return context.UserPrices
			.Where(up => up.DataContextId == dataContext.Id && (!excludeNullPrices || up.Price != null))
			.Include(up => up.ItemOrTag)
			.ThenInclude(i => i.AssociatedItems)
			.ToListAsync();
	}

	public Task<UserPrice?> GetByIdAsync(Guid id)
	{
		return context.UserPrices
			.FirstOrDefaultAsync(up => up.Id == id);
	}

	public UserPrice Add(UserPrice userPrice)
	{
		context.UserPrices.Add(userPrice);
		return userPrice;
	}

	public void Update(UserPrice userPrice)
	{
		if (!context.UserPrices.Contains(userPrice))
		{
			context.UserPrices.Update(userPrice);
		}
	}

	public void Delete(UserPrice userPrice)
	{
		context.UserPrices.Remove(userPrice);
	}
}
