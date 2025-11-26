using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ElementDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericDbService<Element>
{
	public async Task<List<Element>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Elements
			.Include(p => p.Recipe)
			.ToListAsync();
	}

	public async Task<Element?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Elements
			.Include(p => p.Recipe)
			.FirstOrDefaultAsync(p => p.Id == id);
	}

	private Element CloneForDb(Element element)
	{
		return new Element
		{
			Id = element.Id,
			RecipeId = element.Recipe.Id,
			ItemOrTagId = element.ItemOrTag.Id,
			Index = element.Index,
			QuantityId = element.Quantity.Id,
			DefaultIsReintegrated = element.DefaultIsReintegrated,
			DefaultShare = element.DefaultShare,
		};
	}

	public void Create(EcoCraftDbContext context, Element element)
	{
		context.Add(CloneForDb(element));
	}

	public void UpdateAll(EcoCraftDbContext context, Element element)
	{
		context.Attach(CloneForDb(element)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, Element element)
	{
		var entity = new Element { Id = element.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
