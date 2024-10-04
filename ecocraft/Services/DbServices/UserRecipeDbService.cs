using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
    public class UserRecipeDbService : IGenericDbService<UserRecipe>
    {
        private readonly EcoCraftDbContext _context;

        public UserRecipeDbService(EcoCraftDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserRecipe>> GetAllAsync()
        {
            return await _context.UserRecipes
                .Include(up => up.UserServer)
                .ToListAsync();
        }

        public Task<List<UserRecipe>> GetByUserServerAsync(UserServer userServer)
        {
            return _context.UserRecipes
                .Where(s => s.UserServerId == userServer.Id)
                .ToListAsync();
        }

        public async Task<UserRecipe?> GetByIdAsync(Guid id)
        {
            return await _context.UserRecipes
                .Include(up => up.UserServer)
                .FirstOrDefaultAsync(up => up.Id == id);
        }

        public async Task AddAsync(UserRecipe userRecipe)
        {
            await _context.UserRecipes.AddAsync(userRecipe);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(UserRecipe userRecipe)
        {
            _context.UserRecipes.Update(userRecipe);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var userRecipe = await GetByIdAsync(id);
            if (userRecipe != null)
            {
                _context.UserRecipes.Remove(userRecipe);
                await _context.SaveChangesAsync();
            }
        }
    }

}