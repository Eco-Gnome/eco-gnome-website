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
		var trackedEntity = context.UserElements.Local.FirstOrDefault(ue => ue.Id == userElement.Id);

		if (trackedEntity is not null)
		{
			trackedEntity.Price = userElement.Price;
			trackedEntity.IsMarginPrice = userElement.IsMarginPrice;
			trackedEntity.Share = userElement.Share;
			trackedEntity.IsReintegrated = userElement.IsReintegrated;

			var trackedEntry = context.Entry(trackedEntity);
			trackedEntry.Property(x => x.Price).IsModified = true;
			trackedEntry.Property(x => x.IsMarginPrice).IsModified = true;
			trackedEntry.Property(x => x.Share).IsModified = true;
			trackedEntry.Property(x => x.IsReintegrated).IsModified = true;
			return;
		}

		var stub = new UserElement
		{
			Id = userElement.Id,
			Price = userElement.Price,
			IsMarginPrice = userElement.IsMarginPrice,
			Share = userElement.Share,
			IsReintegrated = userElement.IsReintegrated,
		};

		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Price).IsModified = true;
		entry.Property(x => x.IsMarginPrice).IsModified = true;
		entry.Property(x => x.Share).IsModified = true;
		entry.Property(x => x.IsReintegrated).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, UserElement userElement)
	{
		var entity = new UserElement { Id = userElement.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
