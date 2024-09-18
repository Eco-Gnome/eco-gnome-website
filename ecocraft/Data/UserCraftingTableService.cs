using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Data
{
    public class UserCraftingTableService
    {
        private readonly EcoCraftDbContext _context;

        public UserCraftingTableService(EcoCraftDbContext context)
        {
            _context = context;
        }

        // Obtenir toutes les CraftingTables d'un utilisateur
        public async Task<List<UserCraftingTable>> GetUserCraftingTablesAsync(User user)
        {
            return await _context.UserCraftingTables
                .Include(uct => uct.CraftingTable)
                .Include(uct => uct.Upgrade)
                .Where(uct => uct.UserId == user.Id)
                .ToListAsync();
        }

        // Ajouter une nouvelle CraftingTable pour un utilisateur
        public async Task AddUserCraftingTableAsync(UserCraftingTable userCraftingTable)
        {
            _context.UserCraftingTables.Add(userCraftingTable);
            await _context.SaveChangesAsync();
        }

        // Mettre à jour une CraftingTable d'un utilisateur
        public async Task UpdateUserCraftingTableAsync(UserCraftingTable userCraftingTable)
        {
            _context.UserCraftingTables.Update(userCraftingTable);
            await _context.SaveChangesAsync();
        }

        // Supprimer une CraftingTable d'un utilisateur
        public async Task RemoveUserCraftingTableAsync(UserCraftingTable userCraftingTable)
        {
            _context.UserCraftingTables.Remove(userCraftingTable);
            await _context.SaveChangesAsync();
        }

        // Méthode pour mettre à jour les CraftingTables d'un utilisateur
        public async Task UpdateUserCraftingTablesAsync(User user, List<CraftingTable> newCraftingTables)
        {
            // Charger les UserCraftingTables existantes pour cet utilisateur
            var existingUserCraftingTables = await _context.UserCraftingTables
                .Where(uct => uct.UserId == user.Id)
                .ToListAsync();

            // Supprimer les anciennes associations qui ne sont plus dans la liste
            foreach (var existingTable in existingUserCraftingTables)
            {
                if (!newCraftingTables.Any(ct => ct.Id == existingTable.CraftingTableId))
                {
                    _context.UserCraftingTables.Remove(existingTable);
                }
            }

            // Ajouter les nouvelles CraftingTables qui ne sont pas déjà associées
            foreach (var craftingTable in newCraftingTables)
            {
                if (!existingUserCraftingTables.Any(uct => uct.CraftingTableId == craftingTable.Id))
                {
                    var newUserCraftingTable = new UserCraftingTable
                    {
                        UserId = user.Id,
                        CraftingTableId = craftingTable.Id,
                        // Associer un Upgrade si nécessaire (ici, initialisation avec "no upgrade" par défaut)
                        UpgradeId = 5
                    };
                    _context.UserCraftingTables.Add(newUserCraftingTable);
                }
            }

            // Sauvegarder les modifications
            await _context.SaveChangesAsync();
        }

        // Méthode pour récupérer les CraftingTables associées à un utilisateur
        public async Task<List<UserCraftingTable>> GetUserCraftingTablesByUserAsync(User user)
        {
            return await _context.UserCraftingTables
                .Include(uct => uct.CraftingTable)
                .ThenInclude(ct => ct.CraftingTableUpgrades)
                .ThenInclude(ctu => ctu.Upgrade)
                .Include(uct => uct.Upgrade)
                .Where(uct => uct.UserId == user.Id)              
                .ToListAsync();
        }
    }
}
