using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class ServerService : IGenericService<Server>
	{
		private readonly EcoCraftDbContext _context;

		public ServerService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<Server>> GetAllAsync()
		{
			return await _context.Servers.ToListAsync();
		}

		public async Task<Server> GetByIdAsync(Guid id)
		{
			return await _context.Servers.Include(s => s.UserServers)
										  .FirstOrDefaultAsync(s => s.Id == id);
		}

		public async Task AddAsync(Server server)
		{
			await _context.Servers.AddAsync(server);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(Server server)
		{
			_context.Servers.Update(server);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var server = await GetByIdAsync(id);
			if (server != null)
			{
				_context.Servers.Remove(server);
				await _context.SaveChangesAsync();
			}
		}
	}


}
