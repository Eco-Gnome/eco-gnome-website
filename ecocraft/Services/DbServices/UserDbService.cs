using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

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

	public User Add(User user)
	{
		context.Users.Add(user);

		return user;
	}

	public void Update(User user)
	{
		context.Users.Update(user);
	}

	public async Task<User> AddAndSave(User user)
	{
		context.Users.Add(user);

		await context.SaveChangesAsync();

		return user;
	}

	public Task<int> CountUsers()
	{
		return context.Users.CountAsync();
	}

	public Task UpdateAndSave(User user)
	{
		context.Users.Update(user);

		return context.SaveChangesAsync();
	}

	public void Delete(User user)
	{
		context.Users.Remove(user);
	}
}
