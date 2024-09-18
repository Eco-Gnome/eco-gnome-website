using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Data
{
    public class CraftingTableService
    {
        private readonly EcoCraftDbContext _context;

        public CraftingTableService(EcoCraftDbContext context)
        {
            _context = context;
        }

        // Obtenir toutes les tables de crafting
        public async Task<List<CraftingTable>> GetAllCraftingTablesAsync()
        {
            return await _context.CraftingTables
                .Include(ct => ct.Recipes) // Inclure les recettes liées à la table de crafting
                .ToListAsync();
        }

        // Obtenir une table de crafting par son Id
        public async Task<CraftingTable?> GetCraftingTableByIdAsync(int craftingTableId)
        {
            return await _context.CraftingTables
                .Include(ct => ct.Recipes)
                .FirstOrDefaultAsync(ct => ct.Id == craftingTableId);
        }

        // Créer une nouvelle table de crafting
        public async Task CreateCraftingTableAsync(CraftingTable craftingTable)
        {
            _context.CraftingTables.Add(craftingTable);
            await _context.SaveChangesAsync();
        }

        // Mettre à jour une table de crafting existante
        public async Task UpdateCraftingTableAsync(CraftingTable craftingTable)
        {
            _context.CraftingTables.Update(craftingTable);
            await _context.SaveChangesAsync();
        }

        // Supprimer une table de crafting
        public async Task DeleteCraftingTableAsync(int craftingTableId)
        {
            var craftingTable = await _context.CraftingTables.FindAsync(craftingTableId);
            if (craftingTable != null)
            {
                _context.CraftingTables.Remove(craftingTable);
                await _context.SaveChangesAsync();
            }
        }
    }
}
