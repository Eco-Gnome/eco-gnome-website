using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Data
{
    public class UserService
    {
        private readonly EcoCraftDbContext _dbContext;

        public UserService(EcoCraftDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Récupérer un utilisateur par ID
        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _dbContext.Users
								   .Include(u => u.UserSkills)
								   .ThenInclude(us => us.Skill)
								   .ThenInclude(s => s.Recipes)
								   .Include(u => u.UserCraftingTables)
								   .ThenInclude(uct => uct.CraftingTable)
								   .Include(u => u.UserInputPrices)
								   .FirstOrDefaultAsync(u => u.Id == id);
        }

        // Récupérer tous les utilisateurs
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _dbContext.Users
                                   .Include(u => u.UserSkills)
                                   .ThenInclude(us => us.Skill)
                                   .ThenInclude(s => s.Recipes)
								   .Include(u => u.UserCraftingTables)
                                   .ThenInclude(uct => uct.CraftingTable)
								   .Include(u => u.UserInputPrices)
                                   .ToListAsync();
        }

        // Créer un nouvel utilisateur
        public async Task AddUserAsync(User user)
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        // Mettre à jour un utilisateur
        public async Task UpdateUserAsync(User user)
        {
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();
        }

        // Supprimer un utilisateur
        public async Task DeleteUserAsync(int id)
        {
            var user = await GetUserByIdAsync(id);
            if (user != null)
            {
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
