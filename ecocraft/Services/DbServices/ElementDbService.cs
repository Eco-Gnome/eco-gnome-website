using Microsoft.EntityFrameworkCore;
using ecocraft.Models;

namespace ecocraft.Services
{
	public class ElementDbService : IGenericDbService<Element>
	{
		private readonly EcoCraftDbContext _context;

		public ElementDbService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<Element>> GetAllAsync()
		{
			return await _context.Elements.Include(p => p.Recipe)
										  .Include(p => p.ItemOrTag)
										  .Include(p => p.UserElements)
										  .ToListAsync();
		}

		public async Task<Element> GetByIdAsync(Guid id)
		{
			return await _context.Elements.Include(p => p.Recipe)
										  .Include(p => p.ItemOrTag)
										  .Include(p => p.UserElements)
										  .FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task AddAsync(Element element)
		{
			await _context.Elements.AddAsync(element);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Element element)
		{
			_context.Elements.Update(element);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var element = await GetByIdAsync(id);
			if (element != null)
			{
				_context.Elements.Remove(element);
				await _context.SaveChangesAsync();
			}
		}
	}

}
