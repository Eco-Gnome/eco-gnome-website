using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class RecipeDbService(EcoCraftDbContext context) : IGenericNamedDbService<Recipe>
{
	public Task<List<Recipe>> GetAllAsync()
	{
		return context.Recipes
			.Include(r => r.Elements)
			.ToListAsync();
	}

	public Task<List<Recipe>> GetByServerAsync(Server server)
	{
		return context.Recipes
			.Include(c => c.Elements)
			.ThenInclude(e => e.Quantity)
			.ThenInclude(dv => dv.Modifiers)
			.Include(s => s.LocalizedName)
			.Include(s => s.CraftMinutes)
			.ThenInclude(dv => dv.Modifiers)
			.Include(s => s.Labor)
			.ThenInclude(dv => dv.Modifiers)
			.Where(s => s.ServerId == server.Id)
			.ToListAsync();
	}

	public Task<Recipe?> GetByNameAsync(string name)
	{
		return context.Recipes
			.Include(r => r.Elements)
			.FirstOrDefaultAsync(r => r.Name == name);
	}

	public Task<Recipe?> GetByIdAsync(Guid id)
	{
		return context.Recipes
			.Include(r => r.Elements)
			.FirstOrDefaultAsync(r => r.Id == id);
	}

	public Recipe Add(Recipe talent)
	{
		context.Recipes.Add(talent);

		return talent;
	}

	public void Update(Recipe recipe)
	{
		context.Recipes.Update(recipe);
	}

	public void Delete(Recipe recipe)
	{
		context.Recipes.Remove(recipe);
	}
}
