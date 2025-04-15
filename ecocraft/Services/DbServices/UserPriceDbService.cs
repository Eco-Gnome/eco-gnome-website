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
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public Task<UserPrice?> GetByIdAsync(Guid id)
	{
		return context.UserPrices
			.FirstOrDefaultAsync(up => up.Id == id);
	}

	public UserPrice Add(UserPrice talent)
	{
		context.UserPrices.Add(talent);
		talent.ItemOrTag.CurrentUserPrice = talent;

		return talent;
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
		userPrice.ItemOrTag.CurrentUserPrice = null;

		context.UserPrices.Remove(userPrice);
	}
}
