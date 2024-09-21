using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class SkillService : IGenericService<Skill>
	{
		private readonly EcoCraftDbContext _context;

		public SkillService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<Skill>> GetAllAsync()
		{
			return await _context.Skills//.Include(s => s.Recipes)
										 //.Include(s => s.UserSkills)
										 .Include(s => s.Server)
										 .ToListAsync();
		}

		public async Task<Skill> GetByIdAsync(Guid id)
		{
			return await _context.Skills//.Include(s => s.Recipes)
										 //.Include(s => s.UserSkills)
										 .Include(s => s.Server)
										 .FirstOrDefaultAsync(s => s.Id == id);
		}

		public async Task AddAsync(Skill skill)
		{
			await _context.Skills.AddAsync(skill);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Skill skill)
		{
			_context.Skills.Update(skill);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var skill = await GetByIdAsync(id);
			if (skill != null)
			{
				_context.Skills.Remove(skill);
				await _context.SaveChangesAsync();
			}
		}
	}

}
