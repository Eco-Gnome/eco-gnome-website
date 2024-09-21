using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class ItemTagAssocService : IGenericService<ItemTagAssoc>
	{
		private readonly EcoCraftDbContext _context;

		public ItemTagAssocService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<ItemTagAssoc>> GetAllAsync()
		{
			return await _context.ItemTagAssocs
				.Include(ita => ita.Item)
				.Include(ita => ita.Tag)
				.Include(ita => ita.Server)
				.ToListAsync();
		}

		public async Task<ItemTagAssoc> GetByIdAsync(Guid id)
		{
			return await _context.ItemTagAssocs
				.Include(ita => ita.Item)
				.Include(ita => ita.Tag)
				.Include(ita => ita.Server)
				.FirstOrDefaultAsync(ita => ita.Id == id);
		}

		public async Task AddAsync(ItemTagAssoc itemTagAssoc)
		{
			await _context.ItemTagAssocs.AddAsync(itemTagAssoc);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(ItemTagAssoc itemTagAssoc)
		{
			_context.ItemTagAssocs.Update(itemTagAssoc);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var itemTagAssoc = await GetByIdAsync(id);
			if (itemTagAssoc != null)
			{
				_context.ItemTagAssocs.Remove(itemTagAssoc);
				await _context.SaveChangesAsync();
			}
		}
	}
}
