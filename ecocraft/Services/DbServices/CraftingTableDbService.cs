using ecocraft.Models;
using ecocraft.Services.ImportData;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ecocraft.Services
{
	public class CraftingTableDbService : IGenericDbService<CraftingTable>
	{
		private readonly EcoCraftDbContext _context;

		public CraftingTableDbService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public Task<List<CraftingTable>> GetAllAsync()
		{
			return _context.CraftingTables.Include(ct => ct.UserCraftingTables)
												 .Include(ct => ct.Recipes)
												 .Include(ct => ct.PluginModules)
												 .Include(ct => ct.Server)
												 .ToListAsync();
		}

		public Task<List<CraftingTable>> GetByServerAsync(Server server)
		{
			return _context.CraftingTables.Include(c => c.PluginModules)
												.Where(s => s.ServerId == server.Id)
												.ToListAsync();
		}

		public Task<CraftingTable?> GetByIdAsync(Guid id)
		{
			return _context.CraftingTables.Include(ct => ct.UserCraftingTables)
												 .Include(ct => ct.Recipes)
												 .Include(ct => ct.PluginModules)
												 .Include(ct => ct.Server)
												 .FirstOrDefaultAsync(ct => ct.Id == id);
		}
		public Task<CraftingTable?> GetByNameAsync(string name)
		{
			return _context.CraftingTables.Include(ct => ct.UserCraftingTables)
												 .Include(ct => ct.Recipes)
												 .Include(ct => ct.PluginModules)
												 .Include(ct => ct.Server)
												 .FirstOrDefaultAsync(ct => ct.Name == name);
		}

		public async Task AddAsync(CraftingTable craftingTable)
		{
			await _context.CraftingTables.AddAsync(craftingTable);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(CraftingTable craftingTable)
		{
			_context.CraftingTables.Update(craftingTable);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var craftingTable = await GetByIdAsync(id);
			if (craftingTable != null)
			{
				_context.CraftingTables.Remove(craftingTable);
				await _context.SaveChangesAsync();
			}
		}
		public async Task<List<CraftingTable>> GetCraftingTablesForUserSkillsAsync(UserServer userServer, List<Skill> selectedSkill)
		{
			var craftingTables = await _context.CraftingTables
				.Where(ct => ct.ServerId == userServer.ServerId &&  // Filtre par serveur
							 ct.Recipes.Any(r => selectedSkill.Contains(r.Skill) && r.Skill.UserSkills.Any(us => us.UserServer == userServer)))
				.ToListAsync();

			return craftingTables;
		}

	}

}
