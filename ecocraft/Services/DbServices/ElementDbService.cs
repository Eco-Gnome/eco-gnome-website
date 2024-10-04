using Microsoft.EntityFrameworkCore;
using ecocraft.Models;

namespace ecocraft.Services;

public class ElementDbService(EcoCraftDbContext context) : IGenericDbService<Element>
{
	public Task<List<Element>> GetAllAsync()
	{
		return context.Elements.Include(p => p.Recipe)
			.Include(p => p.ItemOrTag)
			.Include(p => p.UserElements)
			.ToListAsync();
	}

	public Task<Element?> GetByIdAsync(Guid id)
	{
		return context.Elements.Include(p => p.Recipe)
			.FirstOrDefaultAsync(p => p.Id == id);
	}

	public Element Add(Element element)
	{
		context.Elements.AddAsync(element);

		return element;
	}

	public void Update(Element element)
	{
		context.Elements.Update(element);
	}

	public void Delete(Element element)
	{
		context.Elements.Remove(element);
	}
}