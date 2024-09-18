using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Data
{
    public class UserInputPriceService
    {
        private readonly EcoCraftDbContext _dbContext;

        public UserInputPriceService(EcoCraftDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Récupérer un prix d'entrée utilisateur par ID
        public async Task<UserInputPrice> GetUserInputPriceByIdAsync(int id)
        {
            return await _dbContext.UserInputPrices
                                   .Include(uip => uip.User)
                                   .FirstOrDefaultAsync(uip => uip.Id == id);
        }

        // Créer un nouveau prix d'entrée utilisateur
        public async Task AddUserInputPriceAsync(UserInputPrice userInputPrice)
        {
            _dbContext.UserInputPrices.Add(userInputPrice);
            await _dbContext.SaveChangesAsync();
        }

        // Mettre à jour un prix d'entrée utilisateur
        public async Task UpdateUserInputPriceAsync(UserInputPrice userInputPrice)
        {
            _dbContext.UserInputPrices.Update(userInputPrice);
            await _dbContext.SaveChangesAsync();
        }

        // Supprimer un prix d'entrée utilisateur
        public async Task DeleteUserInputPriceAsync(int id)
        {
            var userInputPrice = await GetUserInputPriceByIdAsync(id);
            if (userInputPrice != null)
            {
                _dbContext.UserInputPrices.Remove(userInputPrice);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
