using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class SkillDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericNamedDbService<Skill>
{
	public async Task<List<Skill>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Skills
			.ToListAsync();
	}

	public async Task<List<Skill>> GetByServerAsync(Server server, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Skills
			.Where(s => s.ServerId == server.Id)
			.Include(s => s.LocalizedName)
			.Include(s => s.Talents)
			.ThenInclude(t => t.LocalizedName)
			.ToListAsync();
	}

	public async Task<Skill?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Skills
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public async Task<Skill?> GetByNameAsync(string name, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Skills
			.FirstOrDefaultAsync(s => s.Name == name);
	}

	private Skill CloneForDb(Skill skill)
	{
		return new Skill
		{
			Id = skill.Id,
			Name = skill.Name,
			LocalizedNameId = skill.LocalizedName.Id,
			Profession = skill.Profession,
			MaxLevel = skill.MaxLevel,
			LaborReducePercent = skill.LaborReducePercent,
			ServerId = skill.Server.Id,
		};
	}

	public void Create(EcoCraftDbContext context, Skill skill)
	{
		context.Add(CloneForDb(skill));
	}

	public void UpdateAll(EcoCraftDbContext context, Skill skill)
	{
		context.Attach(CloneForDb(skill)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, Skill skill)
	{
		var entity = new Skill { Id = skill.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
