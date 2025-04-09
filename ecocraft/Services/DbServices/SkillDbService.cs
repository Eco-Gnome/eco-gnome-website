using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class SkillDbService(EcoCraftDbContext context)
	: IGenericNamedDbService<Skill>
{
	public Task<List<Skill>> GetAllAsync()
	{
		return context.Skills
			.ToListAsync();
	}

	public Task<List<Skill>> GetByServerAsync(Server server)
	{
		return context.Skills.Where(s => s.ServerId == server.Id)
			.Include(s => s.LocalizedName)
			.Include(s => s.Talents)
			.ThenInclude(t => t.LocalizedName)
			.ToListAsync();
	}

	public Task<Skill?> GetByIdAsync(Guid id)
	{
		return context.Skills
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public Task<Skill?> GetByNameAsync(string name)
	{
		return context.Skills
			.FirstOrDefaultAsync(s => s.Name == name);
	}

	public Skill Add(Skill talent)
	{
		context.Skills.Add(talent);

		return talent;
	}

	public void Update(Skill skill)
	{
		context.Skills.Update(skill);
	}

	public void Delete(Skill skill)
	{
		context.Skills.Remove(skill);
	}
}
