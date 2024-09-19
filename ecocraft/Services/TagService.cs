using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class TagService : IGenericService<Tag>
	{
		private readonly EcoCraftDbContext _context;

		public TagService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<Tag>> GetAllAsync()
		{
			return await _context.Tags
				.Include(t => t.ItemTagAssocs)
				.Include(t => t.Server)
				.ToListAsync();
		}

		public async Task<Tag> GetByIdAsync(Guid id)
		{
			return await _context.Tags
				.Include(t => t.ItemTagAssocs)
				.Include(t => t.Server)
				.FirstOrDefaultAsync(t => t.Id == id);
		}

		public async Task AddAsync(Tag tag)
		{
			await _context.Tags.AddAsync(tag);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Tag tag)
		{
			_context.Tags.Update(tag);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var tag = await GetByIdAsync(id);
			if (tag != null)
			{
				_context.Tags.Remove(tag);
				await _context.SaveChangesAsync();
			}
		}
	}
}
