using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ItemOrTagDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericNamedDbService<ItemOrTag>
{
	public async Task<List<ItemOrTag>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.ItemOrTags
			.ToListAsync();
	}

	public async Task<List<ItemOrTag>> GetByServerAsync(Server server, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.ItemOrTags
			.Include(s => s.AssociatedItems)
			.Include(s => s.AssociatedTags)
			.Include(s => s.LocalizedName)
			.Where(s => s.ServerId == server.Id)
			.ToListAsync();
	}

	public async Task<List<ItemOrTag>> GetWithPriceSetByServerAsync(Server server, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.ItemOrTags
			.Where(s => s.MaxPrice != null || s.MinPrice != null || s.DefaultPrice != null)
			.Where(s => s.ServerId == server.Id)
			.ToListAsync();
	}

	public async Task<ItemOrTag?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.ItemOrTags
			.FirstOrDefaultAsync(i => i.Id == id);
	}

	public async Task<ItemOrTag?> GetByNameAsync(string name, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.ItemOrTags
			.FirstOrDefaultAsync(s => s.Name == name);
	}

	private ItemOrTag CloneForDb(ItemOrTag itemOrTag)
	{
		return new ItemOrTag
		{
			Id = itemOrTag.Id,
			Name = itemOrTag.Name,
			LocalizedNameId = itemOrTag.LocalizedName.Id,
			IsTag = itemOrTag.IsTag,
			MinPrice = itemOrTag.MinPrice,
			DefaultPrice = itemOrTag.DefaultPrice,
			MaxPrice = itemOrTag.MaxPrice,
			ServerId = itemOrTag.Server.Id,
		};
	}

	public void Create(EcoCraftDbContext context, ItemOrTag itemOrTag)
	{
		context.Add(CloneForDb(itemOrTag));
	}

	public void UpdateAll(EcoCraftDbContext context, ItemOrTag itemOrTag)
	{
		context.Attach(CloneForDb(itemOrTag)).State = EntityState.Modified;
	}

	public void UpdatePrices(EcoCraftDbContext context, ItemOrTag itemOrTag)
	{
		var entry = context.Attach(itemOrTag);
		entry.Property(x => x.MinPrice).IsModified = true;
		entry.Property(x => x.DefaultPrice).IsModified = true;
		entry.Property(x => x.MaxPrice).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, ItemOrTag itemOrTag)
	{
		var entity = new ItemOrTag { Id = itemOrTag.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
