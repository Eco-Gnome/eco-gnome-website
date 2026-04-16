using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserSkillDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserSkill>
{
	public async Task<List<UserSkill>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserSkill>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserSkills
			.ToListAsync();
	}

	public async Task<List<UserSkill>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserSkill>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserSkills
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<UserSkill?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserSkill?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserSkills
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	private UserSkill CloneForDb(UserSkill userSkill)
	{
		return new UserSkill
		{
			Id = userSkill.Id,
			SkillId = userSkill.Skill?.Id,
			Level = userSkill.Level,
			DataContextId = userSkill.DataContext.Id,
		};
	}

	public void Create(EcoCraftDbContext context, UserSkill userSkill)
	{
		context.Add(CloneForDb(userSkill));
	}

	public void UpdateAll(EcoCraftDbContext context, UserSkill userSkill)
	{
		context.Attach(CloneForDb(userSkill)).State = EntityState.Modified;
	}

	public void UpdateLevel(EcoCraftDbContext context, UserSkill userSkill)
	{
		var stub = new UserSkill { Id = userSkill.Id, Level = userSkill.Level };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Level).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserSkill userSkill)
	{
		context.QueueDelete<UserSkill>(userSkill.Id);
	}
}
