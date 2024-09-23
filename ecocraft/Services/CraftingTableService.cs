using ecocraft.Models;
using ecocraft.Services.ImportData;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ecocraft.Services
{
	public class CraftingTableService : IGenericService<CraftingTable>
	{
		private readonly EcoCraftDbContext _context;

		public CraftingTableService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<CraftingTable>> GetAllAsync()
		{
			return await _context.CraftingTables.Include(ct => ct.UserCraftingTables)
												 .Include(ct => ct.Recipes)
												 .Include(ct => ct.PluginModules)
												 .Include(ct => ct.Server)
												 .ToListAsync();
		}

		public async Task<CraftingTable> GetByIdAsync(Guid id)
		{
			return await _context.CraftingTables.Include(ct => ct.UserCraftingTables)
												 .Include(ct => ct.Recipes)
												 .Include(ct => ct.PluginModules)
												 .Include(ct => ct.Server)
												 .FirstOrDefaultAsync(ct => ct.Id == id);
		}
		public async Task<CraftingTable> GetByNameAsync(string name)
		{
			return await _context.CraftingTables.Include(ct => ct.UserCraftingTables)
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
		public async Task<List<CraftingTable>> GetCraftingTablesForUserSkillsAsync(User user, Server server, List<Skill> selectedSkill)
		{
			var craftingTables = await _context.CraftingTables
				.Where(ct => ct.Server == server &&  // Filtre par serveur
							 ct.Recipes.Any(r => selectedSkill.Contains(r.Skill) && r.Skill.UserSkills.Any(us => us.User == user)))
				.ToListAsync();

			return craftingTables;
		}

	}

}
