using Microsoft.EntityFrameworkCore;
using ecocraft.Models;

namespace ecocraft.Services
{
	public class ProductService : IGenericService<Product>
	{
		private readonly EcoCraftDbContext _context;

		public ProductService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<Product>> GetAllAsync()
		{
			return await _context.Products.Include(p => p.Recipe)
										  .Include(p => p.Item)
										  //.Include(p => p.UserProducts)
										  .Include(p => p.Server)
										  .ToListAsync();
		}

		public async Task<Product> GetByIdAsync(Guid id)
		{
			return await _context.Products.Include(p => p.Recipe)
										  .Include(p => p.Item)
										  //.Include(p => p.UserProducts)
										  .Include(p => p.Server)
										  .FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task AddAsync(Product product)
		{
			await _context.Products.AddAsync(product);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Product product)
		{
			_context.Products.Update(product);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var product = await GetByIdAsync(id);
			if (product != null)
			{
				_context.Products.Remove(product);
				await _context.SaveChangesAsync();
			}
		}
	}

}
