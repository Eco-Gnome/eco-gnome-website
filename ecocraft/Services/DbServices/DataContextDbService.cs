using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class DataContextDbService(IDbContextFactory<EcoCraftDbContext> factory)
{
	public async Task<List<DataContext>> GetAllAsync(EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.DataContexts
			.ToListAsync();
	}

	public async Task<List<DataContext>> GetByUserServerAsync(UserServer userServer, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.DataContexts
			.Where(s => s.UserServerId == userServer.Id)
			.ToListAsync();
	}

	public async Task<DataContext> GetDataContextWithData(Guid id, Server server)
	{
		await using var context = await factory.CreateDbContextAsync();

		var dataContext = await context.DataContexts
			.AsNoTrackingWithIdentityResolution()
			.Where(s => s.Id == id)
			// User Skills
			.Include(s => s.UserSkills)
			.Include(s => s.UserTalents)
			.Include(s => s.UserCraftingTables)
			.ThenInclude(s => s.SkilledPluginModules)
			.Include(s => s.UserSettings)
			.Include(s => s.UserPrices)
			.Include(s => s.UserRecipes)
			.ThenInclude(s => s.UserElements)
			.Include(s => s.UserMargins)
			.FirstAsync();

		Reconciliate(dataContext, server);

		return dataContext;
	}

	private void Reconciliate(DataContext dataContext, Server server)
	{
		server.Skills.ForEach(s => s.UserSkills.Clear());
		server.Skills.SelectMany(s => s.Talents).ToList().ForEach(t => t.UserTalents.Clear());
		server.CraftingTables.ForEach(s => s.UserCraftingTables.Clear());
		server.PluginModules.ForEach(s => s.UserCraftingTables.Clear());
		server.ItemOrTags.ForEach(s => s.UserPrices.Clear());
		server.Recipes.ForEach(s => s.UserRecipes.Clear());
		server.Recipes.SelectMany(s => s.Elements).ToList().ForEach(s => s.UserElements.Clear());

		var skills = server.Skills.ToDictionary(s => s.Id);
		var talents = server.Skills.SelectMany(s => s.Talents).ToDictionary(s => s.Id);
		var craftingTables = server.CraftingTables.ToDictionary(s => s.Id);
		var pluginModules = server.PluginModules.ToDictionary(s => s.Id);
		var itemOrTags = server.ItemOrTags.ToDictionary(s => s.Id);
		var recipes = server.Recipes.ToDictionary(s => s.Id);
		var elements = server.Recipes.SelectMany(s => s.Elements).ToDictionary(s => s.Id);


		dataContext.UserSkills.ForEach(us =>
		{
			if (us.SkillId is not null)
			{
				us.Skill = skills[us.SkillId.Value];
				us.Skill.UserSkills.Add(us);
			}
		});

		dataContext.UserTalents.ForEach(ut =>
		{
			ut.Talent = talents[ut.TalentId];
			ut.Talent.UserTalents.Add(ut);
		});

		dataContext.UserCraftingTables.ForEach(uct =>
		{
			uct.CraftingTable = craftingTables[uct.CraftingTableId];
			uct.CraftingTable.UserCraftingTables.Add(uct);
			if (uct.PluginModuleId is not null)
			{
				uct.PluginModule = pluginModules[uct.PluginModuleId.Value];
				uct.PluginModule.UserCraftingTables.Add(uct);
			}
			uct.SkilledPluginModules = uct.SkilledPluginModules.Select(s => server.PluginModules.First(pm => pm.Id == s.Id)).ToList();
			// We don't care about the reverse of the skilledPluginModules
		});

		dataContext.UserPrices.ForEach(up =>
		{
			up.ItemOrTag = itemOrTags[up.ItemOrTagId];
			up.ItemOrTag.UserPrices.Add(up);
		});

		dataContext.UserRecipes.ForEach(ur =>
		{
			ur.Recipe = recipes[ur.RecipeId];
			ur.Recipe.UserRecipes.Add(ur);
		});

		dataContext.UserElements.ForEach(ue =>
		{
			ue.Element = elements[ue.ElementId];
			ue.Element.UserElements.Add(ue);
		});
	}

	public async Task<DataContext?> GetByIdAsync(Guid id, EcoCraftDbContext? context = null)
	{
		context ??= await factory.CreateDbContextAsync();

		return await context.DataContexts
			.FirstOrDefaultAsync(us => us.Id == id);
	}

	private DataContext CloneForDb(DataContext dataContext)
	{
		return new DataContext
		{
			Id = dataContext.Id,
			UserServerId = dataContext.UserServer.Id,
			Name = dataContext.Name,
			IsDefault =	dataContext.IsDefault,
			IsShoppingList = dataContext.IsShoppingList,
		};
	}

	public void Create(EcoCraftDbContext context, DataContext dataContext)
	{
		context.Add(CloneForDb(dataContext));
	}

	public void UpdateAll(EcoCraftDbContext context, DataContext dataContext)
	{
		context.Attach(CloneForDb(dataContext)).State = EntityState.Modified;
	}

	public void UpdateName(EcoCraftDbContext context, DataContext dataContext)
	{
		var entry = context.Attach(dataContext);
		entry.Property(x => x.Name).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, DataContext dataContext)
	{
		var entity = new DataContext { Id = dataContext.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
