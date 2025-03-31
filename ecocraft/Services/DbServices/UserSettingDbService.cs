using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserSettingDbService(EcoCraftDbContext context) : IGenericDbService<UserSetting>
{
	public Task<List<UserSetting>> GetAllAsync()
	{
		return context.UserSettings
			.ToListAsync();
	}

	public Task<UserSetting?> GetByUserServerAsync(UserServer userServer)
	{
		return context.UserSettings
			.FirstOrDefaultAsync(us => us.UserServerId == userServer.Id);
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