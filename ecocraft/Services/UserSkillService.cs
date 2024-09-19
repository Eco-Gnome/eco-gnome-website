using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserSkillService : IGenericService<UserSkill>
	{
		private readonly EcoCraftDbContext _context;

		public UserSkillService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<UserSkill>> GetAllAsync()
		{
			return await _context.UserSkills.Include(us => us.User)
											.Include(us => us.Skill)
											.Include(us => us.Server)
											.ToListAsync();
		}

		public async Task<UserSkill> GetByIdAsync(Guid id)
		{
			return await _context.UserSkills.Include(us => us.User)
											.Include(us => us.Skill)
											.Include(us => us.Server)
											.FirstOrDefaultAsync(us => us.Id == id);
		}

		public async Task AddAsync(UserSkill userSkill)
		{
			await _context.UserSkills.AddAsync(userSkill);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserSkill userSkill)
		{
			_context.UserSkills.Update(userSkill);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userSkill = await GetByIdAsync(id);
			if (userSkill != null)
			{
				_context.UserSkills.Remove(userSkill);
				await _context.SaveChangesAsync();
			}
		}
	}


}
