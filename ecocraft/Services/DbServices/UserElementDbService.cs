using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserElementDbService : IGenericDbService<UserElement>
	{
		private readonly EcoCraftDbContext _context;

		public UserElementDbService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<List<UserElement>> GetAllAsync()
		{
			return await _context.UserElements.Include(ue => ue.UserServer)
											  .Include(ue => ue.Element)
											  .ToListAsync();
		}

		public Task<List<UserElement>> GetByUserServerAsync(UserServer userServer)
		{
			return _context.UserElements
				.Where(s => s.UserServerId == userServer.Id)
				.ToListAsync();
		}

		public async Task<UserElement> GetByIdAsync(Guid id)
		{
			return await _context.UserElements.Include(ue => ue.UserServer)
											  .Include(ue => ue.Element)
											  .FirstOrDefaultAsync(ue => ue.Id == id);
		}

		public async Task AddAsync(UserElement userElement)
		{
			await _context.UserElements.AddAsync(userElement);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserElement userElement)
		{
			_context.UserElements.Update(userElement);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userElement = await GetByIdAsync(id);
			if (userElement != null)
			{
				_context.UserElements.Remove(userElement);
				await _context.SaveChangesAsync();
			}
		}
	}


}
