using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services
{
	public class UserSettingDbService(EcoCraftDbContext context) : IGenericUserDbService<UserSetting>
	{
		public Task<List<UserSetting>> GetAllAsync()
		{
			return context.UserSettings
				.ToListAsync();
		}

		public Task<List<UserSetting>> GetByUserServerAsync(UserServer userServer)
		{
			return context.UserSettings
				.Where(s => s.UserServerId == userServer.Id)
				.ToListAsync();
		}

		public Task<UserSetting?> GetByIdAsync(Guid id)
		{
			return context.UserSettings
				.FirstOrDefaultAsync(us => us.Id == id);
		}

		public UserSetting Add(UserSetting userSetting)
		{
			context.UserSettings.Add(userSetting);

			return userSetting;
		}

		public void Update(UserSetting userSetting)
		{
			context.UserSettings.Update(userSetting);
		}

		public void Delete(UserSetting userSetting)
		{
			context.UserSettings.Remove(userSetting);
		}
	}

}
