using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class UserElementDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericUserDbService<UserElement>
{
	public async Task<List<UserElement>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<UserElement>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.UserElements
			.ToListAsync();
	}

	public async Task<List<UserElement>> GetByDataContextAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByDataContextAsync(dataContext, context);
	}

	public async Task<List<UserElement>> GetByDataContextAsync(DataContext dataContext, EcoCraftDbContext context)
	{
		return await context.UserElements
			.Where(s => s.DataContextId == dataContext.Id)
			.ToListAsync();
	}

	public async Task<List<UserElement>> GetByDataContextForEcoApiAsync(DataContext dataContext)
	{
		await using var context = await factory.CreateDbContextAsync();

		return await context.UserElements
			.Where(up => up.DataContextId == dataContext.Id)
			.Include(ue => ue.Element)
			.ThenInclude(e => e.Recipe)
			.ThenInclude(r => r.Skill)
			.Include(ue => ue.Element)
			.ThenInclude(e => e.Quantity)
			.ToListAsync();
	}

	public async Task<UserElement?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<UserElement?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
		return await context.UserElements
			.FirstOrDefaultAsync(ue => ue.Id == id);
	}

	private UserElement CloneForDb(UserElement userElement)
	{
		return new UserElement
		{
			Id = userElement.Id,
			ElementId = userElement.Element.Id,
			Price = userElement.Price,
			IsMarginPrice = userElement.IsMarginPrice,
			Share = userElement.Share,
			IsReintegrated = userElement.IsReintegrated,
			DataContextId = userElement.DataContext.Id,
			UserRecipeId = userElement.UserRecipe.Id,
		};
	}

	public void Create(EcoCraftDbContext context, UserElement userElement)
	{
		context.Add(CloneForDb(userElement));
	}

	public void UpdateAll(EcoCraftDbContext context, UserElement userElement)
	{
		context.Attach(CloneForDb(userElement)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, UserElement userElement)
	{
		var entity = new UserElement { Id = userElement.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
