using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Data
{
    public class UserSkillService
    {
        private readonly EcoCraftDbContext _dbContext;

        public UserSkillService(EcoCraftDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Récupérer une compétence utilisateur par ID
        public async Task<UserSkill?> GetUserSkillByIdAsync(int id)
        {
            return await _dbContext.UserSkills
                                   .Include(us => us.User)
                                   .Include(us => us.Skill)
                                   .FirstOrDefaultAsync(us => us.Id == id);
        }

        // Créer une nouvelle compétence utilisateur
        public async Task AddUserSkillAsync(UserSkill userSkill)
        {
            _dbContext.UserSkills.Add(userSkill);
            await _dbContext.SaveChangesAsync();
        }

        // Mettre à jour une compétence utilisateur
        public async Task UpdateUserSkillAsync(UserSkill userSkill)
        {
            _dbContext.UserSkills.Update(userSkill);
            await _dbContext.SaveChangesAsync();
        }

        // Supprimer une compétence utilisateur
        public async Task DeleteUserSkillAsync(int id)
        {
            var userSkill = await GetUserSkillByIdAsync(id);
            if (userSkill != null)
            {
                _dbContext.UserSkills.Remove(userSkill);
                await _dbContext.SaveChangesAsync();
            }
        }

        // Récupérer les compétences d'un utilisateur
        public async Task<List<UserSkill>> GetUserSkillsByUserIdAsync(int userId)
        {
            return await _dbContext.UserSkills
                                   .Include(us => us.Skill)
                                   .Where(us => us.UserId == userId)
                                   .ToListAsync();
        }

        // Méthode pour récupérer les compétences d'un utilisateur via l'objet utilisateur
        public async Task<List<UserSkill>> GetUserSkillsByUserAsync(User user)
        {
            return await _dbContext.UserSkills
                                    .Include(us => us.Skill)
                                    .Include(us => us.Skill.CraftingTableSkills)
                                    .ThenInclude(cts => cts.CraftingTable)
                                    .Where(us => us.UserId == user.Id)                                   
                                    .ToListAsync();
        }

        // Ajouter ou mettre à jour les compétences d'un utilisateur
        public async Task UpdateUserSkillsAsync(int userId, List<Skill> selectedSkills)
        {
            // Récupérer les compétences actuelles de l'utilisateur
            var existingUserSkills = await GetUserSkillsByUserIdAsync(userId);

            // Supprimer les compétences non sélectionnées
            var skillsToRemove = existingUserSkills.Where(us => !selectedSkills.Any(s => s.Id == us.SkillId)).ToList();
            _dbContext.UserSkills.RemoveRange(skillsToRemove);

            // Ajouter les nouvelles compétences sélectionnées
            foreach (var skill in selectedSkills)
            {
                if (!existingUserSkills.Any(us => us.SkillId == skill.Id))
                {
                    var newUserSkill = new UserSkill
                    {
                        UserId = userId,
                        SkillId = skill.Id,
                        Level = 0 // Vous pouvez ajuster le niveau par défaut
                    };
                    _dbContext.UserSkills.Add(newUserSkill);
                }
            }

            // Sauvegarder les modifications
            await _dbContext.SaveChangesAsync();
        }

        // Méthode pour mettre à jour les compétences de l'utilisateur
        public async Task UpdateUserSkillsAsync(User user, List<Skill> selectedSkills)
        {
            // Récupérer les compétences actuelles de l'utilisateur
            var existingUserSkills = await _dbContext.UserSkills
                                                     .Where(us => us.UserId == user.Id)
                                                     .ToListAsync();

            // Supprimer les compétences non sélectionnées
            var skillsToRemove = existingUserSkills.Where(us => !selectedSkills.Any(s => s.Id == us.SkillId)).ToList();
            _dbContext.UserSkills.RemoveRange(skillsToRemove);

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
                    _dbContext.UserSkills.Add(newUserSkill);
                }
            }

            // Sauvegarder les modifications
            await _dbContext.SaveChangesAsync();
        }

        // Supprimer toutes les compétences d'un utilisateur (optionnel)
        public async Task RemoveAllUserSkillsAsync(int userId)
        {
            var userSkills = await GetUserSkillsByUserIdAsync(userId);
            _dbContext.UserSkills.RemoveRange(userSkills);
            await _dbContext.SaveChangesAsync();
        }
    }
}
