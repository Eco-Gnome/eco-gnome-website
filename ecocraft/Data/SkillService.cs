using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Data
{
    public class SkillService
    {
        private readonly EcoCraftDbContext _dbContext;

        public SkillService(EcoCraftDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Récupérer toutes les compétences
        public async Task<List<Skill>> GetAllSkillsAsync()
        {
            return await _dbContext.Skills.ToListAsync();
        }

        public async Task<List<UserSkill>> GetSkillsByUserAsync(User user)
        {
            return await _dbContext.UserSkills
                                   .Where(us => us.UserId == user.Id)
                                   .ToListAsync();
        }
    }
}
