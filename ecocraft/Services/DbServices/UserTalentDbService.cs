using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserTalentDbService(EcoCraftDbContext context) : IGenericUserDbService<UserTalent>
{
	public Task<List<UserTalent>> GetAllAsync()
	{
		return context.UserTalents
			.ToListAsync();
	}

	public Task<List<UserTalent>> GetByDataContextAsync(DataContext dataContext)
	{
		return context.UserTalents
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public Task<UserTalent?> GetByIdAsync(Guid id)
	{
		return context.UserTalents
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	public UserTalent Add(UserTalent talent)
	{
		context.UserTalents.Add(talent);

		if (talent.Talent is not null)
		{
			talent.Talent.CurrentUserTalent = talent;
		}

		return talent;
	}

	public void Update(UserTalent userTalent)
	{
		context.UserTalents.Update(userTalent);
	}

	public void Delete(UserTalent userTalent)
	{
		if (userTalent.Talent is not null)
		{
			userTalent.Talent.CurrentUserTalent = null;
		}

		context.UserTalents.Remove(userTalent);
	}
}
