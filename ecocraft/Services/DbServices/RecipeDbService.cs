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
			.Include(s => s.LocalizedName)
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

	public Recipe Add(Recipe recipe)
	{
		context.Recipes.Add(recipe);

		return recipe;
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