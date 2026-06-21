using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserAutomationInputDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserAutomationInput>
{
	public async Task<List<UserAutomationInput>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserAutomationInput>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserAutomationInputs
			.ToListAsync();
	}

	public async Task<List<UserAutomationInput>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserAutomationInput>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserAutomationInputs
			.Where(uai => uai.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<UserAutomationInput?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserAutomationInput?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserAutomationInputs
			.FirstOrDefaultAsync(uai => uai.Id == id);
	}

	private UserAutomationInput CloneForDb(UserAutomationInput userAutomationInput)
	{
		return new UserAutomationInput
		{
			Id = userAutomationInput.Id,
			DataContextId = userAutomationInput.DataContext.Id,
			ItemOrTagId = userAutomationInput.ItemOrTag.Id,
			Cap = userAutomationInput.Cap,
		};
	}

	public void Create(EcoCraftDbContext context, UserAutomationInput userAutomationInput)
	{
		context.Add(CloneForDb(userAutomationInput));
	}

	public void UpdateCap(EcoCraftDbContext context, UserAutomationInput userAutomationInput)
	{
		var stub = new UserAutomationInput { Id = userAutomationInput.Id, Cap = userAutomationInput.Cap };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Cap).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserAutomationInput userAutomationInput)
	{
		context.QueueDelete<UserAutomationInput>(userAutomationInput.Id);
	}
}
