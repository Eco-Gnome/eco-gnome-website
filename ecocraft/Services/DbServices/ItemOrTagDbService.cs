using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

public class ItemOrTagDbService(EcoCraftDbContext context) : IGenericNamedDbService<ItemOrTag>
{
	public Task<List<ItemOrTag>> GetAllAsync()
	{
		return context.ItemOrTags
			.ToListAsync();
	}

	public Task<List<ItemOrTag>> GetByServerAsync(Server server)
	{
		return context.ItemOrTags
			.Where(s => s.ServerId == server.Id)
			.ToListAsync();
	}

	public Task<ItemOrTag?> GetByIdAsync(Guid id)
	{
		return context.ItemOrTags
			.FirstOrDefaultAsync(i => i.Id == id);
	}
		
	public Task<ItemOrTag?> GetByNameAsync(string name)
	{
		return context.ItemOrTags
			.FirstOrDefaultAsync(s => s.Name == name);
	}
		
	public ItemOrTag Add(ItemOrTag itemOrTag)
	{
		context.ItemOrTags.Add(itemOrTag);

		return itemOrTag;
	}

	public void Update(ItemOrTag itemOrTag)
	{
		context.ItemOrTags.Update(itemOrTag);
	}

	public void Delete(ItemOrTag itemOrTag)
	{
		context.ItemOrTags.Remove(itemOrTag);
	}
}