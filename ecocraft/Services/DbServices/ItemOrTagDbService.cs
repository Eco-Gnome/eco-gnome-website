using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class ItemOrTagDbService : IGenericDbService<ItemOrTag>
	{
		private readonly EcoCraftDbContext _context;

		public ItemOrTagDbService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<ItemOrTag>> GetAllAsync()
		{
			return await _context.ItemOrTags
				.Include(i => i.UserPrices)
				.Include(i => i.Server)
				.ToListAsync();
		}

		public Task<List<ItemOrTag>> GetByServerAsync(Server server)
		{
			return _context.ItemOrTags
				.Include(i => i.AssociatedItemOrTags)
				.Where(s => s.ServerId == server.Id)
				.ToListAsync();
		}

		public async Task<ItemOrTag?> GetByIdAsync(Guid id)
		{
			return await _context.ItemOrTags
				.Include(i => i.UserPrices)
				.Include(i => i.Server)
				.FirstOrDefaultAsync(i => i.Id == id);
		}
		
		public async Task<ItemOrTag?> GetByNameAsync(string name)
		{
			return await _context.ItemOrTags
				.Include(s => s.Server)
				.FirstOrDefaultAsync(s => s.Name == name);
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
