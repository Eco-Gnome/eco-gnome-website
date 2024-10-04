using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
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
			context.UserPrices.Update(userPrice);
		}

		public void Delete(UserPrice userPrice)
		{
			context.UserPrices.Remove(userPrice);
		}
	}

}
