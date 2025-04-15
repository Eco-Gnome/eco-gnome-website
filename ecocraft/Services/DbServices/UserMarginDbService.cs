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

	public Task<List<UserMargin>> GetByDataContextAsync(DataContext dataContext)
	{
        return context.UserMargins
            .Where(s => s.DataContextId == dataContext.Id)
            .ToListAsync();
    }

	public Task<UserMargin?> GetByIdAsync(Guid id)
	{
		return context.UserMargins
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	public UserMargin Add(UserMargin talent)
	{
		context.UserMargins.Add(talent);

		return talent;
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
