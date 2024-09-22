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

		public async Task<List<UserSkill>> GetAllAsync()
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

		// Méthode pour mettre à jour les compétences de l'utilisateur
		public async Task UpdateUserSkillsAsync(User user, List<Skill> selectedSkills)
		{
			// Récupérer les compétences actuelles de l'utilisateur
			var existingUserSkills = await _context.UserSkills
													 .Where(us => us.UserId == user.Id)
													 .ToListAsync();

			// Supprimer les compétences non sélectionnées
			var skillsToRemove = existingUserSkills.Where(us => !selectedSkills.Any(s => s.Id == us.SkillId)).ToList();
			_context.UserSkills.RemoveRange(skillsToRemove);

			// Ajouter les nouvelles compétences sélectionnées
			foreach (var skill in selectedSkills)
			{
				if (!existingUserSkills.Any(us => us.SkillId == skill.Id))
				{
					var newUserSkill = new UserSkill
					{
						UserId = user.Id,
						SkillId = skill.Id,
						Level = 0 // Ajuster le niveau si nécessaire
					};
					_context.UserSkills.Add(newUserSkill);
				}
			}
		}

		// Méthode pour récupérer les compétences d'un utilisateur via l'objet utilisateur
		public async Task<List<UserSkill>> GetUserSkillsByUserAsync(User user)
		{
			return await _context.UserSkills
									.Include(us => us.Skill)
									//.Include(us => us.Skill.CraftingTableSkills)
									//.ThenInclude(cts => cts.CraftingTable)
									.Where(us => us.UserId == user.Id)
									.ToListAsync();
		}
	}

}
