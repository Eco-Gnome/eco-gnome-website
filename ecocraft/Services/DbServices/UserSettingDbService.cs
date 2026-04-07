using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserSettingDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericDbService<UserSetting>
{
	public async Task<List<UserSetting>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserSetting>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserSettings
			.ToListAsync();
	}

	public async Task<UserSetting?> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<UserSetting?> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserSettings
			.FirstOrDefaultAsync(us => us.DataContextId == dataContext.Id);
	}

	public async Task<UserSetting?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserSetting?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserSettings
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	private UserSetting CloneForDb(UserSetting userSetting)
	{
		return new UserSetting
		{
			Id = userSetting.Id,
			DataContextId = userSetting.DataContext.Id,
			MarginType = userSetting.MarginType,
			CalorieCost = userSetting.CalorieCost,
			DisplayNonSkilledRecipes = userSetting.DisplayNonSkilledRecipes,
			OnlyLevelAccessibleRecipes = userSetting.OnlyLevelAccessibleRecipes,
			ApplyMarginBetweenSkills = userSetting.ApplyMarginBetweenSkills,
		};
	}

	public void Create(EcoCraftDbContext context, UserSetting userSetting)
	{
		context.Add(CloneForDb(userSetting));
	}

	public void UpdateAll(EcoCraftDbContext context, UserSetting userSetting)
	{
		context.Attach(CloneForDb(userSetting)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, UserSetting userSetting)
	{
		var entity = new UserSetting { Id = userSetting.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
