using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserTalentDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserTalent>
{
	public async Task<List<UserTalent>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserTalent>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserTalents
			.ToListAsync();
	}

	public async Task<List<UserTalent>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserTalent>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserTalents
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<UserTalent?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserTalent?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserTalents
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	private UserTalent CloneForDb(UserTalent userTalent)
	{
		return new UserTalent
		{
			Id = userTalent.Id,
			TalentId = userTalent.Talent.Id,
			Level = userTalent.Level,
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

	public void UpdateLevel(EcoCraftDbContext context, UserTalent userTalent)
	{
		var stub = new UserTalent { Id = userTalent.Id, Level = userTalent.Level };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Level).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserTalent userTalent)
	{
		var entity = new UserTalent { Id = userTalent.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
