using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class RecipeDbService(IDbContextFactory<EcoCraftDbContext> factory) : IGenericNamedDbService<Recipe>
{
	public async Task<List<Recipe>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Recipes
			.Include(r => r.Elements)
			.ToListAsync();
	}

	public async Task<List<Recipe>> GetByServerAsync(Server server, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Recipes
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

	public async Task<Recipe?> GetByNameAsync(string name, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Recipes
			.Include(r => r.Elements)
			.FirstOrDefaultAsync(r => r.Name == name);
	}

	public async Task<Recipe?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.Recipes
			.Include(r => r.Elements)
			.FirstOrDefaultAsync(r => r.Id == id);
	}

	private Recipe CloneForDb(Recipe recipe)
	{
		return new Recipe
		{
			Id = recipe.Id,
			Name = recipe.Name,
			LocalizedNameId = recipe.LocalizedName.Id,
			FamilyName = recipe.FamilyName,
			CraftMinutesId = recipe.CraftMinutes.Id,
			SkillId = recipe.Skill?.Id,
			SkillLevel = recipe.SkillLevel,
			IsBlueprint = recipe.IsBlueprint,
			IsDefault = recipe.IsDefault,
			LaborId = recipe.Labor.Id,
			CraftingTableId = recipe.CraftingTable.Id,
			ServerId = recipe.Server.Id,
		};
	}

	public void Create(EcoCraftDbContext context, Recipe recipe)
	{
		context.Add(CloneForDb(recipe));
	}

	public void UpdateAll(EcoCraftDbContext context, Recipe recipe)
	{
		context.Attach(CloneForDb(recipe)).State = EntityState.Modified;
	}

	public void Destroy(EcoCraftDbContext context, Recipe recipe)
	{
		var entity = new Recipe { Id = recipe.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
