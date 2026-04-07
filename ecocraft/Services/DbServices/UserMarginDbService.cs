using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserMarginDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericDbService<UserMargin>
{
	public async Task<List<UserMargin>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserMargin>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserMargins
			.ToListAsync();
	}

	public async Task<List<UserMargin>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserMargin>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
        return await context.UserMargins
            .Where(s => s.DataContextId == dataContext.Id)
            .ToListAsync();
    }

	public async Task<UserMargin?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserMargin?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserMargins
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	private UserMargin CloneForDb(UserMargin userMargin)
	{
		return new UserMargin
		{
			Id = userMargin.Id,
			DataContextId = userMargin.DataContext.Id,
			Name = userMargin.Name,
			Margin = userMargin.Margin,
		};
	}

	public void Create(EcoCraftDbContext context, UserMargin userMargin)
	{
		context.Add(CloneForDb(userMargin));
	}

	public void UpdateAll(EcoCraftDbContext context, UserMargin userMargin)
	{
		context.Attach(CloneForDb(userMargin)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, UserMargin userMargin)
	{
		var entity = new UserMargin { Id = userMargin.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
