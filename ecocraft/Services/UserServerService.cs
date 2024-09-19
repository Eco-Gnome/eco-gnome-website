using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserServerService : IGenericService<UserServer>
	{
		private readonly EcoCraftDbContext _context;

		public UserServerService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<UserServer>> GetAllAsync()
		{
			return await _context.UserServers.Include(us => us.User)
											  .Include(us => us.Server)
											  .ToListAsync();
		}

		public async Task<UserServer> GetByIdAsync(Guid id)
		{
			return await _context.UserServers.Include(us => us.User)
											  .Include(us => us.Server)
											  .FirstOrDefaultAsync(us => us.Id == id);
		}

		public async Task AddAsync(UserServer userServer)
		{
			await _context.UserServers.AddAsync(userServer);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserServer userServer)
		{
			_context.UserServers.Update(userServer);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userServer = await GetByIdAsync(id);
			if (userServer != null)
			{
				_context.UserServers.Remove(userServer);
				await _context.SaveChangesAsync();
			}
		}
	}

}
