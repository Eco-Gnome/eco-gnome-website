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
			return await _context.ItemsOrTags.Include(i => i.Products)
											 .Include(i => i.Ingredients)
											 .Include(i => i.UserPrices)
											 .Include(i => i.ItemTagAssocs)
											 .Include(i => i.Server)
											 .ToListAsync();
		}

		public async Task<ItemOrTag> GetByIdAsync(Guid id)
		{
			return await _context.ItemsOrTags.Include(i => i.Products)
											 .Include(i => i.Ingredients)
											 .Include(i => i.UserPrices)
											 .Include(i => i.ItemTagAssocs)
											 .Include(i => i.Server)
											 .FirstOrDefaultAsync(i => i.Id == id);
		}

		public async Task AddAsync(ItemOrTag itemOrTag)
		{
			await _context.ItemsOrTags.AddAsync(itemOrTag);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(ItemOrTag itemOrTag)
		{
			_context.ItemsOrTags.Update(itemOrTag);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var itemOrTag = await GetByIdAsync(id);
			if (itemOrTag != null)
			{
				_context.ItemsOrTags.Remove(itemOrTag);
				await _context.SaveChangesAsync();
			}
		}
	}


}
