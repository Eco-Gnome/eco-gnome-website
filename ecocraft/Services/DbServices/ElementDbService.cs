using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ElementDbService(EcoCraftDbContext context) : IGenericDbService<Element>
{
	public Task<List<Element>> GetAllAsync()
	{
		return context.Elements.Include(p => p.Recipe)
			.ToListAsync();
	}

	public Task<Element?> GetByIdAsync(Guid id)
	{
		return context.Elements.Include(p => p.Recipe)
			.FirstOrDefaultAsync(p => p.Id == id);
	}

	public Element Add(Element talent)
	{
		context.Elements.Add(talent);

		return talent;
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
