using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserMarginDbService(EcoCraftDbContext context) : IGenericDbService<UserMargin>
{
	public Task<List<UserMargin>> GetAllAsync()
	{
		return context.UserMargins
			.ToListAsync();
	}

	public Task<List<UserMargin>> GetByUserServerAsync(UserServer userServer)
	{
        return context.UserMargins
            .Where(s => s.UserServerId == userServer.Id)
            .ToListAsync();
    }

	public Task<UserMargin?> GetByIdAsync(Guid id)
	{
		return context.UserMargins
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	public UserMargin Add(UserMargin UserMargin)
	{
		context.UserMargins.Add(UserMargin);

		return UserMargin;
	}

	public void Update(UserMargin UserMargin)
	{
		context.UserMargins.Update(UserMargin);
	}

	public void Delete(UserMargin UserMargin)
	{
		context.UserMargins.Remove(UserMargin);
	}
}