using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserTalentDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserTalent>
{
	public async Task<List<UserTalent>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.UserTalents
			.ToListAsync();
	}

	public async Task<List<UserTalent>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.UserTalents
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<UserTalent?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.UserTalents
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	private UserTalent CloneForDb(UserTalent userTalent)
	{
		return new UserTalent
		{
			Id = userTalent.Id,
			TalentId = userTalent.Talent.Id,
			DataContextId = userTalent.DataContext.Id,
		};
	}

	public void Create(EcoCraftDbContext context, UserTalent userTalent)
	{
		context.Add(CloneForDb(userTalent));
	}

	public void UpdateAll(EcoCraftDbContext context, UserTalent userTalent)
	{
		context.Attach(CloneForDb(userTalent)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, UserTalent userTalent)
	{
		var entity = new UserTalent { Id = userTalent.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
