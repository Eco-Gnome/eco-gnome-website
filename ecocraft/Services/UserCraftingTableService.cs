using ecocraft.Models;
using Microsoft.EntityFrameworkCore;



namespace ecocraft.Services
{
	public class UserCraftingTableService : IGenericService<UserCraftingTable>
	{
		private readonly EcoCraftDbContext _context;
		private readonly PluginModuleService _pluginModuleService;

		public UserCraftingTableService(PluginModuleService pluginModuleService, EcoCraftDbContext context)
		{
			_pluginModuleService = pluginModuleService;
			_context = context;
		}

		public async Task<List<UserCraftingTable>> GetAllAsync()
		{
			return await _context.UserCraftingTables.Include(uct => uct.User)
													 .Include(uct => uct.CraftingTable)
													 .Include(uct => uct.PluginModule)
													 .Include(uct => uct.Server)
													 .ToListAsync();
		}

		public async Task<UserCraftingTable> GetByIdAsync(Guid id)
		{
			return await _context.UserCraftingTables.Include(uct => uct.User)
													 .Include(uct => uct.CraftingTable)
													 .Include(uct => uct.PluginModule)
													 .Include(uct => uct.Server)
													 .FirstOrDefaultAsync(uct => uct.Id == id);
		}

		public async Task AddAsync(UserCraftingTable userCraftingTable)
		{
			await _context.UserCraftingTables.AddAsync(userCraftingTable);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserCraftingTable userCraftingTable)
		{
			_context.UserCraftingTables.Update(userCraftingTable);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userCraftingTable = await GetByIdAsync(id);
			if (userCraftingTable != null)
			{
				_context.UserCraftingTables.Remove(userCraftingTable);
				await _context.SaveChangesAsync();
			}
		}

		// Méthode pour mettre à jour les CraftingTables d'un utilisateur
		public async Task UpdateUserCraftingTablesAsync(User user, Server server, List<CraftingTable> newCraftingTables)
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

			PluginModule defaultModule = await _pluginModuleService.GetByNameAsync("NoUpgrade");

			// Ajouter les nouvelles CraftingTables qui ne sont pas déjà associées
			foreach (var craftingTable in newCraftingTables)
			{
				if (!existingUserCraftingTables.Any(uct => uct.CraftingTableId == craftingTable.Id))
				{
					var newUserCraftingTable = new UserCraftingTable
					{
						User = user,
						CraftingTable = craftingTable,
						Server = server,
						// Associer un Upgrade si nécessaire (ici, initialisation avec "no upgrade" par défaut)
						//UpgradeId = 5
						PluginModule = defaultModule
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
				.ThenInclude(ct => ct.CraftingTablePluginModules)
				.ThenInclude(ctu => ctu.PluginModule)
				.Include(uct => uct.PluginModule)
				.Where(uct => uct.UserId == user.Id)
				.ToListAsync();
		}
	}

}
