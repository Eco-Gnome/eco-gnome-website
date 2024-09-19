using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class IngredientService : IGenericService<Ingredient>
	{
		private readonly EcoCraftDbContext _context;

		public IngredientService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<Ingredient>> GetAllAsync()
		{
			return await _context.Ingredients.Include(i => i.Recipe)
											  .Include(i => i.ItemOrTag)
											  .Include(i => i.Server)
											  .ToListAsync();
		}

		public async Task<Ingredient> GetByIdAsync(Guid id)
		{
			return await _context.Ingredients.Include(i => i.Recipe)
											  .Include(i => i.ItemOrTag)
											  .Include(i => i.Server)
											  .FirstOrDefaultAsync(i => i.Id == id);
		}

		public async Task AddAsync(Ingredient ingredient)
		{
			await _context.Ingredients.AddAsync(ingredient);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Ingredient ingredient)
		{
			_context.Ingredients.Update(ingredient);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var ingredient = await GetByIdAsync(id);
			if (ingredient != null)
			{
				_context.Ingredients.Remove(ingredient);
				await _context.SaveChangesAsync();
			}
		}
	}

}
