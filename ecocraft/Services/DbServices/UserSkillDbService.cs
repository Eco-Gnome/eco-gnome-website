using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserSkillDbService : IGenericDbService<UserSkill>
	{
		private readonly EcoCraftDbContext _context;

		public UserSkillDbService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<UserSkill>> GetAllAsync()
		{
			return await _context.UserSkills.Include(us => us.UserServer)
											.Include(us => us.Skill)
											.ToListAsync();
		}

		public Task<List<UserSkill>> GetByUserServerAsync(UserServer userServer)
		{
			return _context.UserSkills
				.Where(s => s.UserServerId == userServer.Id)
				.ToListAsync();
		}

		public async Task<UserSkill> GetByIdAsync(Guid id)
		{
			return await _context.UserSkills.Include(us => us.UserServer)
											.Include(us => us.Skill)
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

		// Méthode pour récupérer les compétences d'un utilisateur via l'objet utilisateur
		public async Task<List<UserSkill>> GetUserSkillsByUserAsync(UserServer userServer)
		{
			return await _context.UserSkills
									.Include(us => us.Skill)
									//.Include(us => us.Skill.CraftingTableSkills)
									//.ThenInclude(cts => cts.CraftingTable)
									.Where(us => us.UserServerId == userServer.Id)
									.ToListAsync();
		}
	}

}
