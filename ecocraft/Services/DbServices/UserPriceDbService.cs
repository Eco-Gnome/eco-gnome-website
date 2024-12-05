using ecocraft.Models;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace ecocraft.Services.DbServices;

public class UserPriceDbService(EcoCraftDbContext context) : IGenericUserDbService<UserPrice>
{
	public Task<List<UserPrice>> GetAllAsync()
	{
		return context.UserPrices
			.ToListAsync();
	}

	public Task<List<UserPrice>> GetByUserServerAsync(UserServer userServer)
	{
		return context.UserPrices
			.Where(s => s.UserServerId == userServer.Id)
			.ToListAsync();
	}

	public Task<List<UserPrice>> GetByServerIdAndEcoUserId(Guid serverId, string ecoUserId)
	{
		return context.UserPrices
			.Include(up => up.UserServer)
			.Where(up => up.UserServer.ServerId == serverId && up.UserServer.EcoUserId == ecoUserId)
			.Include(up => up.ItemOrTag)
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
		userPrice.ItemOrTag.CurrentUserPrice = userPrice;

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
		userPrice.ItemOrTag.CurrentUserPrice = null;

		context.UserPrices.Remove(userPrice);
	}
}
