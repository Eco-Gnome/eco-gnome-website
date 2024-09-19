using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class ItemOrTagService : IGenericService<ItemOrTag>
	{
		private readonly EcoCraftDbContext _context;

		public ItemOrTagService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<ItemOrTag>> GetAllAsync()
		{
			return await _context.ItemOrTags
				.Include(i => i.Products)
				.Include(i => i.Ingredients)
				.Include(i => i.UserPrices)
				.Include(i => i.Items) // Association avec ItemTagAssoc
				.Include(i => i.Tags)          // Association avec Tags
				.Include(i => i.Server)
				.ToListAsync();
		}

		public async Task<ItemOrTag> GetByIdAsync(Guid id)
		{
			return await _context.ItemOrTags
				.Include(i => i.Products)
				.Include(i => i.Ingredients)
				.Include(i => i.UserPrices)
				.Include(i => i.Items)
				.Include(i => i.Tags)
				.Include(i => i.Server)
				.FirstOrDefaultAsync(i => i.Id == id);
		}

		public async Task AddAsync(ItemOrTag itemOrTag)
		{
			await _context.ItemOrTags.AddAsync(itemOrTag);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(ItemOrTag itemOrTag)
		{
			_context.ItemOrTags.Update(itemOrTag);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var itemOrTag = await GetByIdAsync(id);
			if (itemOrTag != null)
			{
				_context.ItemOrTags.Remove(itemOrTag);
				await _context.SaveChangesAsync();
			}
		}
	}
}
