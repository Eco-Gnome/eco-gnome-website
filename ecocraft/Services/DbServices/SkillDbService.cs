using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class SkillDbService : IGenericNamedDbService<Skill>
{
	private readonly EcoCraftDbContext _context;

	public SkillDbService(EcoCraftDbContext context)
	{
		_context = context;
	}

	public Task<List<Skill>> GetAllAsync()
	{
		return _context.Skills
			.ToListAsync();
	}

	public Task<List<Skill>> GetByServerAsync(Server server)
	{
		return _context.Skills.Where(s => s.ServerId == server.Id)
			.ToListAsync();
	}

	public Task<Skill?> GetByIdAsync(Guid id)
	{
		return _context.Skills
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public Task<Skill?> GetByNameAsync(string name)
	{
		return _context.Skills
			.FirstOrDefaultAsync(s => s.Name == name);
	}

	public Skill Add(Skill skill)
	{
		_context.Skills.Add(skill);
		
		return skill;
	}

	public void Update(Skill skill)
	{
		_context.Skills.Update(skill);
	}

	public void Delete(Skill skill)
	{
		_context.Skills.Remove(skill);
	}
}