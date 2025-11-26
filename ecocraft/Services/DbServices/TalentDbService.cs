using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class TalentDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericNamedDbService<Talent>
{
	public async Task<List<Talent>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Talents
			.ToListAsync();
	}

	public async Task<List<Talent>> GetBySkillAsync(Skill skill, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Talents
			.Where(s => s.SkillId == skill.Id)
			.Include(s => s.LocalizedName)
			.ToListAsync();
	}

	public async Task<Talent?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Talents
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public async Task<Talent?> GetByNameAsync(string name, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Talents
			.FirstOrDefaultAsync(s => s.Name == name);
	}

	private Talent CloneForDb(Talent talent)
	{
		return new Talent
		{
			Id = talent.Id,
			Name = talent.Name,
			LocalizedNameId = talent.LocalizedName.Id,
			TalentGroupName = talent.TalentGroupName,
			Value = talent.Value,
			Level = talent.Level,
			SkillId = talent.Skill.Id,
		};
	}

	public void Create(EcoCraftDbContext context, Talent talent)
	{
		context.Add(CloneForDb(talent));
	}

	public void UpdateAll(EcoCraftDbContext context, Talent talent)
	{
		context.Attach(CloneForDb(talent)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, Talent talent)
	{
		var entity = new Talent { Id = talent.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
