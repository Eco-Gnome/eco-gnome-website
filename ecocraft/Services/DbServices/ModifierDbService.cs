using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class ModifierDbService(EcoCraftDbContext context)
{
	public Task<List<Modifier>> GetAllAsync()
	{
		return context.Modifiers
			.ToListAsync();
	}

	public Task<Modifier?> GetByIdAsync(Guid id)
	{
		return context.Modifiers
			.FirstOrDefaultAsync(s => s.Id == id);
	}

	public Modifier Add(Modifier modifier)
	{
		context.Modifiers.Add(modifier);

		return modifier;
	}

	public void Update(Modifier modifier)
	{
		context.Modifiers.Update(modifier);
	}

	public void Delete(Modifier modifier)
	{
		context.Modifiers.Remove(modifier);
	}
}
