using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserSettingService : IGenericService<UserSetting>
	{
		private readonly EcoCraftDbContext _context;

		public UserSettingService(EcoCraftDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<UserSetting>> GetAllAsync()
		{
			return await _context.UserSettings.Include(us => us.User)
											   .Include(us => us.Server)
											   .ToListAsync();
		}

		public async Task<UserSetting> GetByIdAsync(Guid id)
		{
			return await _context.UserSettings.Include(us => us.User)
											   .Include(us => us.Server)
											   .FirstOrDefaultAsync(us => us.Id == id);
		}

		public async Task AddAsync(UserSetting userSetting)
		{
			await _context.UserSettings.AddAsync(userSetting);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(UserSetting userSetting)
		{
			_context.UserSettings.Update(userSetting);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var userSetting = await GetByIdAsync(id);
			if (userSetting != null)
			{
				_context.UserSettings.Remove(userSetting);
				await _context.SaveChangesAsync();
			}
		}
	}

}
