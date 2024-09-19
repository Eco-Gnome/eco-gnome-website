using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserProductService : IGenericService<UserProduct>
	{
		private readonly EcoCraftDbContext _context;

		public UserProductService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<UserProduct>> GetAllAsync()
		{
			return await _context.UserProducts.Include(up => up.User)
											  .Include(up => up.Product)
											  .Include(up => up.Server)
											  .ToListAsync();
		}

		public async Task<UserProduct> GetByIdAsync(Guid id)
		{
			return await _context.UserProducts.Include(up => up.User)
											  .Include(up => up.Product)
											  .Include(up => up.Server)
											  .FirstOrDefaultAsync(up => up.Id == id);
		}

		public async Task AddAsync(UserProduct userProduct)
		{
			await _context.UserProducts.AddAsync(userProduct);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserProduct userProduct)
		{
			_context.UserProducts.Update(userProduct);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userProduct = await GetByIdAsync(id);
			if (userProduct != null)
			{
				_context.UserProducts.Remove(userProduct);
				await _context.SaveChangesAsync();
			}
		}
	}


}
