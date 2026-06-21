using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services.DbServices;

public class DataContextDbService(IDbContextFactory<EcoCraftDbContext> factory)
{
	public async Task<List<DataContext>> GetAllAsync()
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetAllAsync(context);
	}

	public async Task<List<DataContext>> GetAllAsync(EcoCraftDbContext context)
	{
		return await context.DataContexts
			.ToListAsync();
	}

	public async Task<List<DataContext>> GetByUserServerAsync(UserServer userServer)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByUserServerAsync(userServer, context);
	}

	public async Task<List<DataContext>> GetByUserServerAsync(UserServer userServer, EcoCraftDbContext context)
	{
		return await context.DataContexts
			.Where(s => s.UserServerId == userServer.Id)
			.ToListAsync();
	}

	public async Task<DataContext> GetDataContextWithData(Guid id, Server server)
	{
		await using var context = await factory.CreateDbContextAsync();

		var dataContext = await context.DataContexts
			.AsNoTrackingWithIdentityResolution()
			.AsSplitQuery()
			.Where(s => s.Id == id)
			// User Skills
			.Include(s => s.UserSkills)
			.Include(s => s.UserTalents)
			.Include(s => s.UserCraftingTables)
			.ThenInclude(s => s.SkilledPluginModules)
			.Include(s => s.UserCraftingTables)
			.ThenInclude(s => s.FuelItem)
			.Include(s => s.UserSettings)
			.Include(s => s.UserPrices)
			.Include(s => s.UserRecipes)
			.ThenInclude(s => s.UserElements)
			.Include(s => s.UserMargins)
			.Include(s => s.UserAutomationInputs)
			.Include(s => s.UserAutomationTargets)
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
		server.ItemOrTags.ForEach(s => s.UserCraftingTables.Clear());
		server.Recipes.ForEach(s => s.UserRecipes.Clear());
		server.Recipes.SelectMany(s => s.Elements).ToList().ForEach(s => s.UserElements.Clear());
		dataContext.UserMargins.ForEach(um => um.UserPrices.Clear());

		var skills = server.Skills.ToDictionary(s => s.Id);
		var talents = server.Skills.SelectMany(s => s.Talents).ToDictionary(s => s.Id);
		var craftingTables = server.CraftingTables.ToDictionary(s => s.Id);
		var pluginModules = server.PluginModules.ToDictionary(s => s.Id);
		var itemOrTags = server.ItemOrTags.ToDictionary(s => s.Id);
		var recipes = server.Recipes.ToDictionary(s => s.Id);
		var elements = server.Recipes.SelectMany(s => s.Elements).ToDictionary(s => s.Id);
		var userMargins = dataContext.UserMargins.ToDictionary(um => um.Id);
		var userRecipes = dataContext.UserRecipes.ToDictionary(ur => ur.Id);


		dataContext.UserSkills.ToList().ForEach(us =>
		{
			if (us.SkillId is null)
			{
				return;
			}

			if (!skills.TryGetValue(us.SkillId.Value, out var skill))
			{
				dataContext.UserSkills.Remove(us);
				return;
			}

			us.Skill = skill;
			us.Skill.UserSkills.Add(us);
		});

		dataContext.UserTalents.ToList().ForEach(ut =>
		{
			if (!talents.TryGetValue(ut.TalentId, out var talent))
			{
				dataContext.UserTalents.Remove(ut);
				return;
			}

			ut.Talent = talent;
			ut.Talent.UserTalents.Add(ut);
		});

		dataContext.UserCraftingTables.ToList().ForEach(uct =>
		{
			if (!craftingTables.TryGetValue(uct.CraftingTableId, out var craftingTable))
			{
				dataContext.UserCraftingTables.Remove(uct);
				return;
			}

			uct.CraftingTable = craftingTable;
			uct.CraftingTable.UserCraftingTables.Add(uct);

			if (uct.PluginModuleId is Guid pluginModuleId && pluginModules.TryGetValue(pluginModuleId, out var pluginModule))
			{
				uct.PluginModule = pluginModule;
				uct.PluginModule.UserCraftingTables.Add(uct);
			}
			else
			{
				uct.PluginModule = null;
				uct.PluginModuleId = null;
			}

			if (uct.FuelItemId is Guid fuelItemId && itemOrTags.TryGetValue(fuelItemId, out var fuelItem) && !fuelItem.IsTag)
			{
				uct.FuelItem = fuelItem;
				uct.FuelItem.UserCraftingTables.Add(uct);
			}
			else
			{
				uct.FuelItem = null;
				uct.FuelItemId = null;
			}

			uct.SkilledPluginModules = uct.SkilledPluginModules
				.Where(spm => pluginModules.ContainsKey(spm.Id))
				.Select(spm => pluginModules[spm.Id])
				.ToList();
			// We don't care about the reverse of the skilledPluginModules
		});

		dataContext.UserPrices.ToList().ForEach(up =>
		{
			if (!itemOrTags.TryGetValue(up.ItemOrTagId, out var itemOrTag))
			{
				dataContext.UserPrices.Remove(up);
				return;
			}

			up.ItemOrTag = itemOrTag;
			up.ItemOrTag.UserPrices.Add(up);

			up.DataContext = dataContext;
			if (up.UserMarginId is Guid userMarginId && userMargins.TryGetValue(userMarginId, out var userMargin))
			{
				up.UserMargin = userMargin;
				up.UserMargin.UserPrices.Add(up);
			}
			else
			{
				up.UserMargin = null;
				up.UserMarginId = null;
			}
		});

		dataContext.UserAutomationInputs.ToList().ForEach(uai =>
		{
			if (!itemOrTags.TryGetValue(uai.ItemOrTagId, out var itemOrTag))
			{
				dataContext.UserAutomationInputs.Remove(uai);
				return;
			}

			uai.ItemOrTag = itemOrTag;
			uai.DataContext = dataContext;
		});

		dataContext.UserAutomationTargets.ToList().ForEach(uat =>
		{
			if (!itemOrTags.TryGetValue(uat.ItemOrTagId, out var itemOrTag))
			{
				dataContext.UserAutomationTargets.Remove(uat);
				return;
			}

			uat.ItemOrTag = itemOrTag;
			uat.DataContext = dataContext;
		});

		dataContext.UserRecipes.ToList().ForEach(ur =>
		{
			if (!recipes.TryGetValue(ur.RecipeId, out var recipe))
			{
				dataContext.UserRecipes.Remove(ur);
				userRecipes.Remove(ur.Id);
				return;
			}

			ur.DataContext = dataContext;
			ur.Recipe = recipe;
			ur.Recipe.UserRecipes.Add(ur);
			ur.ParentUserRecipe = null;
			ur.ChildrenUserRecipes.Clear();
			ur.UserElements.Clear();
		});

		dataContext.UserRecipes.ForEach(ur =>
		{
			if (ur.ParentUserRecipeId is Guid parentId && userRecipes.TryGetValue(parentId, out var parent))
			{
				ur.ParentUserRecipe = parent;
				parent.ChildrenUserRecipes.Add(ur);
			}
		});

		dataContext.UserElements.ToList().ForEach(ue =>
		{
			if (!elements.TryGetValue(ue.ElementId, out var element) || !userRecipes.TryGetValue(ue.UserRecipeId, out var userRecipe))
			{
				dataContext.UserElements.Remove(ue);
				return;
			}

			ue.DataContext = dataContext;
			ue.Element = element;
			ue.UserRecipe = userRecipe;
			ue.UserPricesPrimaryOf.Clear();
			ue.Element.UserElements.Add(ue);
			userRecipe.UserElements.Add(ue);
		});

		var userElements = dataContext.UserElements.ToDictionary(ue => ue.Id);
		var userPrices = dataContext.UserPrices.ToDictionary(up => up.Id);
		dataContext.UserPrices.ForEach(up =>
		{
			if (up.PrimaryUserPriceId is Guid primaryUserPriceId && userPrices.TryGetValue(primaryUserPriceId, out var primaryUserPrice))
			{
				up.PrimaryUserPrice = primaryUserPrice;
			}
			else
			{
				up.PrimaryUserPrice = null;
				up.PrimaryUserPriceId = null;
			}

			if (up.PrimaryUserElementId is Guid primaryUserElementId && userElements.TryGetValue(primaryUserElementId, out var primaryUserElement))
			{
				up.PrimaryUserElement = primaryUserElement;
				up.PrimaryUserElement.UserPricesPrimaryOf.Add(up);
			}
			else
			{
				up.PrimaryUserElement = null;
				up.PrimaryUserElementId = null;
			}
		});
	}

	public async Task<DataContext?> GetByIdAsync(Guid id)
	{
		await using var context = await factory.CreateDbContextAsync();
		return await GetByIdAsync(id, context);
	}

	public async Task<DataContext?> GetByIdAsync(Guid id, EcoCraftDbContext context)
	{
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
		var stub = new DataContext { Id = dataContext.Id, Name = dataContext.Name };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.Name).IsModified = true;
	}

	public void UpdateIsDefault(EcoCraftDbContext context, DataContext dataContext)
	{
		var stub = new DataContext { Id = dataContext.Id, IsDefault = dataContext.IsDefault };
		var entry = context.Entry(stub);
		entry.State = EntityState.Unchanged;
		entry.Property(x => x.IsDefault).IsModified = true;
	}

	public void Destroy(EcoCraftDbContext context, DataContext dataContext)
	{
		var entity = new DataContext { Id = dataContext.Id };
		context.Entry(entity).State = EntityState.Deleted;
	}
}
