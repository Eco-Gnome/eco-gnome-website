using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserAutomationTargetDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserAutomationTarget>
{
	public async Task<List<UserAutomationTarget>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserAutomationTarget>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserAutomationTargets
			.ToListAsync();
	}

	public async Task<List<UserAutomationTarget>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserAutomationTarget>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserAutomationTargets
			.Where(uat => uat.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<UserAutomationTarget?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserAutomationTarget?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserAutomationTargets
			.FirstOrDefaultAsync(uat => uat.Id == id);
	}

	private UserAutomationTarget CloneForDb(UserAutomationTarget userAutomationTarget)
	{
		return new UserAutomationTarget
		{
			Id = userAutomationTarget.Id,
			DataContextId = userAutomationTarget.DataContext.Id,
			ItemOrTagId = userAutomationTarget.ItemOrTag.Id,
			Rate = userAutomationTarget.Rate,
			IsMax = userAutomationTarget.IsMax,
		};
	}

	public void Create(EcoCraftDbContext context, UserAutomationTarget userAutomationTarget)
	{
		context.Add(CloneForDb(userAutomationTarget));
	}

	// Le débit cible et le mode « max » changent indépendamment côté UI ; on marque les deux modifiés
	// pour qu'un seul appel couvre les deux cas (saisie d'un débit / bascule max).
	public void Update(EcoCraftDbContext context, UserAutomationTarget userAutomationTarget)
	{
		var stub = new UserAutomationTarget
		{
			Id = userAutomationTarget.Id,
			Rate = userAutomationTarget.Rate,
			IsMax = userAutomationTarget.IsMax,
		};
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Rate).IsModified = true;
		entry.Property(x => x.IsMax).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserAutomationTarget userAutomationTarget)
	{
		context.QueueDelete<UserAutomationTarget>(userAutomationTarget.Id);
	}
}
