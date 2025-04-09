using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class TalentDbService(EcoCraftDbContext context)
	: IGenericNamedDbService<Talent>
{
	public Task<List<Talent>> GetAllAsync()
	{
		return context.Talents
			.ToListAsync();
	}

	public Task<List<Talent>> GetBySkillAsync(Skill skill)
	{
		return context.Talents.Where(s => s.SkillId == skill.Id)
			.Include(s => s.LocalizedName)
			.ToListAsync();
	}

	public Task<Talent?> GetByIdAsync(Guid id)
	{
		return context.Talents
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public Task<Talent?> GetByNameAsync(string name)
	{
		return context.Talents
			.FirstOrDefaultAsync(s => s.Name == name);
	}

	public Talent Add(Talent talent)
	{
		context.Talents.Add(talent);

		return talent;
	}

	public void Update(Talent talent)
	{
		context.Talents.Update(talent);
	}

	public void Delete(Talent talent)
	{
		context.Talents.Remove(talent);
	}
}
