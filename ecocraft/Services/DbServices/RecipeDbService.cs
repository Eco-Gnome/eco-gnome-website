using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class RecipeDbService : IGenericDbService<Recipe>
	{
		private readonly EcoCraftDbContext _context;

		public RecipeDbService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<Recipe>> GetAllAsync()
		{
			return await _context.Recipes.Include(r => r.Elements)
										  .Include(r => r.Skill)
										  .Include(r => r.CraftingTable)
										  .Include(r => r.Server)
										  .ToListAsync();
		}

		public Task<List<Recipe>> GetByServerAsync(Server server)
		{
			return _context.Recipes.Include(c => c.Elements)
				.Where(s => s.ServerId == server.Id)
				.ToListAsync();
		}

		public async Task<Recipe> GetByIdAsync(Guid id)
		{
			return await _context.Recipes.Include(r => r.Elements)
										  .Include(r => r.Skill)
										  .Include(r => r.CraftingTable)
										  .Include(r => r.Server)
										  .FirstOrDefaultAsync(r => r.Id == id);
		}

		public async Task AddAsync(Recipe recipe)
		{
			await _context.Recipes.AddAsync(recipe);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Recipe recipe)
		{
			_context.Recipes.Update(recipe);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var recipe = await GetByIdAsync(id);
			if (recipe != null)
			{
				_context.Recipes.Remove(recipe);
				await _context.SaveChangesAsync();
			}
		}
	}


}
