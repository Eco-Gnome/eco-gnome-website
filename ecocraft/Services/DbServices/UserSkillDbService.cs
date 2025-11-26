using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserSkillDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserSkill>
{
	public async Task<List<UserSkill>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.UserSkills
			.ToListAsync();
	}

	public async Task<List<UserSkill>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.UserSkills
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<UserSkill?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

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
		var entry = context.Attach(userSkill);
		entry.Property(x => x.Level).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserSkill userSkill)
	{
		var entity = new UserSkill { Id = userSkill.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
