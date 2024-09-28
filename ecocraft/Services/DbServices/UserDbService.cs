using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserDbService(EcoCraftDbContext context) : IGenericDbService<User>
	{
		public Task<List<User>> GetAllAsync()
		{
			return context.Users.Include(u => u.UserServers)
								.ThenInclude(us => us.Server)
								.ToListAsync();
		}

		public Task<User?> GetByIdAsync(Guid id)
		{
			return context.Users.Include(u => u.UserServers)
								.ThenInclude(us => us.Server)
								.FirstOrDefaultAsync(u => u.Id == id);
		}

		public async Task AddAsync(User user)
		{
			await context.Users.AddAsync(user);
			await context.SaveChangesAsync();
		}

		public async Task UpdateAsync(User user)
		{
			context.Users.Update(user);
			await context.SaveChangesAsync();
		}

		public async Task SaveAsync()
		{
			await context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var user = await GetByIdAsync(id);
			if (user != null)
			{
				context.Users.Remove(user);
				await context.SaveChangesAsync();
			}
		}
	}


}
