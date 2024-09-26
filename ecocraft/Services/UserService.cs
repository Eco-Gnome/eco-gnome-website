using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{


	public class UserService : IGenericService<User>
	{
		private readonly EcoCraftDbContext _context;

		public UserService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<User>> GetAllAsync()
		{
			return await _context.Users.Include(u => u.UserServers)
										.ToListAsync();
		}

		public async Task<User> GetByIdAsync(Guid id)
		{
			return await _context.Users.Include(u => u.UserServers)
										.FirstOrDefaultAsync(u => u.Id == id);
		}

		public async Task AddAsync(User user)
		{
			await _context.Users.AddAsync(user);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(User user)
		{
			_context.Users.Update(user);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var user = await GetByIdAsync(id);
			if (user != null)
			{
				_context.Users.Remove(user);
				await _context.SaveChangesAsync();
			}
		}
	}


}
