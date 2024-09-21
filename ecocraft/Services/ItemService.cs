using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class ItemService : IGenericService<Item>
	{
		private readonly EcoCraftDbContext _context;

		public ItemService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<Item>> GetAllAsync()
		{
			return await _context.Items
				.Include(i => i.Products)
				.Include(i => i.Ingredients)
				.Include(i => i.UserPrices)
				.Include(i => i.ItemTagAssocs)
				.Include(i => i.Server)
				.ToListAsync();
		}

		public async Task<Item> GetByIdAsync(Guid id)
		{
			return await _context.Items
				.Include(i => i.Products)
				.Include(i => i.Ingredients)
				.Include(i => i.UserPrices)
				.Include(i => i.ItemTagAssocs)
				.Include(i => i.Server)
				.FirstOrDefaultAsync(i => i.Id == id);
		}

		public async Task AddAsync(Item item)
		{
			await _context.Items.AddAsync(item);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Item item)
		{
			_context.Items.Update(item);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var item = await GetByIdAsync(id);
			if (item != null)
			{
				_context.Items.Remove(item);
				await _context.SaveChangesAsync();
			}
		}
	}
}
