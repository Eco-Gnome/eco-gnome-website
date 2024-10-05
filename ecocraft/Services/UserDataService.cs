using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ecocraft.Services;

public class UserDataService(
	UserSkillDbService userSkillDbService,
    UserCraftingTableDbService userCraftingTableDbService,
    UserSettingDbService userSettingDbService,
    UserElementDbService userElementDbService,
    UserPriceDbService userPriceDbService,
    UserRecipeDbService userRecipeDbService,
    ServerDataService serverDataService,
	UserDbService userDbService)
{
    public List<UserSkill> UserSkills { get; private set; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; private set; } = [];
    public List<UserSetting> UserSettings { get; private set; } = [];
    public List<UserElement> UserElements { get; private set; } = [];
    public List<UserPrice> UserPrices { get; private set; } = [];
    public List<UserRecipe> UserRecipes { get; private set; } = [];

    public async Task RetrieveUserData(UserServer userServer)
    {
        var userSkillsTask = userSkillDbService.GetByUserServerAsync(userServer);
        var userCraftingTablesTask = userCraftingTableDbService.GetByUserServerAsync(userServer);
        var userSettingsTask = userSettingDbService.GetByUserServerAsync(userServer);
        var userElementsTask = userElementDbService.GetByUserServerAsync(userServer);
        var userPricesTask = userPriceDbService.GetByUserServerAsync(userServer);
        var userRecipesTask = userRecipeDbService.GetByUserServerAsync(userServer);
        
        await Task.WhenAll(userSkillsTask, userCraftingTablesTask, userSettingsTask, userElementsTask, userPricesTask, userRecipesTask);

        UserSkills = userSkillsTask.Result;
        UserCraftingTables = userCraftingTablesTask.Result;
        UserSettings = userSettingsTask.Result;
        UserElements = userElementsTask.Result;
        UserPrices = userPricesTask.Result;
        UserRecipes = userRecipesTask.Result;
    }

	public async Task SaveUserData(UserServer userServer)
	{
		userServer.UserSkills = UserSkills;
		userServer.UserCraftingTables = UserCraftingTables;
		userServer.UserSettings = UserSettings;
		userServer.UserElements = UserElements;
		userServer.UserPrices = UserPrices;
		userServer.UserRecipes = UserRecipes;

		await userDbService.UpdateAndSave(userServer.User);

    }


	public void AddUserSkill(UserSkill userSkill)
    {
		UserSkills.Add(userSkill);
		userSkillDbService.Add(userSkill);
	}

    public void RemoveUserSkill(UserSkill userSkill)
    {
        UserSkills.Remove(userSkill);
        userSkillDbService.Delete(userSkill);
    }

    public void AddUserCraftingTable(UserCraftingTable userCraftingTable)
    {
		UserCraftingTables.Add(userCraftingTable);
		userCraftingTableDbService.Add(userCraftingTable);
	}

    public void RemoveUserCraftingTable(UserCraftingTable userCraftingTable)
    {
        UserCraftingTables.Remove(userCraftingTable);
        userCraftingTableDbService.Delete(userCraftingTable);
    }

    public void AddUserRecipe(UserRecipe userRecipe)
    {
		UserRecipes.Add(userRecipe);
		userRecipeDbService.Add(userRecipe);
	}

    public void RemoveUserRecipe(UserRecipe userRecipe)
    {
        UserRecipes.Remove(userRecipe);
        userRecipeDbService.Delete(userRecipe);
    }

	public List<UserCraftingTable> UpdateUserCraftingTables(UserServer userServer)
	{
		// 1. Récupérer les compétences actuelles de l'utilisateur
		var userSkills = UserSkills.Where(us => us.UserServer.Id == userServer.Id).Select(us => us.Skill).ToList();

		// 2. Récupérer toutes les CraftingTables disponibles en fonction des compétences de l'utilisateur
		var availableCraftingTables = new List<CraftingTable>();
		foreach (var skill in userSkills)
		{
			// Ajouter toutes les CraftingTables liées aux recettes que l'utilisateur peut réaliser via ses compétences
			var craftingTables = skill.Recipes
				.Select(r => r.CraftingTable)
				.Distinct()
				.ToList();
			availableCraftingTables.AddRange(craftingTables);
		}

		// 3. Charger les UserCraftingTables existantes pour cet utilisateur
		var existingUserCraftingTables = UserCraftingTables.Where(uct => uct.UserServer.Id == userServer.Id).ToList();

		// 4. Retirer les UserCraftingTables qui ne sont plus accessibles via les compétences
		var craftingTablesToRemove = existingUserCraftingTables
			.Where(uct => !availableCraftingTables.Any(ct => ct.Id == uct.CraftingTable.Id))
			.ToList();

		foreach (var existingTable in craftingTablesToRemove)
		{
			UserCraftingTables.Remove(existingTable);
		}

		// 5. Ajouter les nouvelles CraftingTables qui ne sont pas déjà associées à l'utilisateur
		PluginModule defaultModule = userServer.Server.PluginModules.FirstOrDefault(pm => pm.Name.Equals("NoUpgrade"));

		foreach (var craftingTable in availableCraftingTables)
		{
			if (!existingUserCraftingTables.Any(uct => uct.CraftingTable.Id == craftingTable.Id))
			{
				var newUserCraftingTable = new UserCraftingTable
				{
					UserServer = userServer,
					CraftingTable = craftingTable,
					PluginModule = defaultModule // Associer un PluginModule par défaut
				};
				UserCraftingTables.Add(newUserCraftingTable);
			}
		}

		// Retourner la liste mise à jour des UserCraftingTables
		return UserCraftingTables;
	}

	public List<UserRecipe> UpdateUserRecipes(UserServer userServer)
	{
		// 1. Récupérer les compétences actuelles de l'utilisateur et leur niveau
		var userSkills = UserSkills.Where(us => us.UserServer.Id == userServer.Id).ToList();

		// 2. Récupérer les CraftingTables disponibles pour cet utilisateur
		var craftingTables = UserCraftingTables
			.Where(uct => uct.UserServer.Id == userServer.Id)
			.Select(uct => uct.CraftingTable)
			.ToList();

		// 3. Charger les UserRecipes existants pour cet utilisateur
		var existingUserRecipes = UserRecipes.Where(ur => ur.UserServer.Id == userServer.Id).ToList();

		// Liste pour les nouvelles recettes
		var availableRecipes = new List<Recipe>();

		// 4. Récupérer les recettes disponibles en fonction des compétences de l'utilisateur et des CraftingTables
		foreach (var userSkill in userSkills)
		{
			var skill = userSkill.Skill;
			var skillLevel = userSkill.Level;

			// Ajouter les recettes qui correspondent au niveau de compétence de l'utilisateur
			var validRecipes = skill.Recipes
				.Where(r => r.SkillLevel <= skillLevel) // Limité au niveau de compétence
				.Where(r => craftingTables.Any(ct => ct.Id == r.CraftingTable.Id)) // Table d'artisanat disponible
				.ToList();

			availableRecipes.AddRange(validRecipes);
		}

		// 5. Supprimer les UserRecipes qui ne sont plus accessibles
		var recipesToRemove = existingUserRecipes
			.Where(ur => !availableRecipes.Any(ar => ar.Id == ur.Recipe.Id))
			.ToList();

		foreach (var recipeToRemove in recipesToRemove)
		{
			UserRecipes.Remove(recipeToRemove);
		}

		// 6. Ajouter les nouvelles UserRecipes qui ne sont pas déjà associées à l'utilisateur
		foreach (var recipe in availableRecipes)
		{
			if (!existingUserRecipes.Any(ur => ur.Recipe.Id == recipe.Id))
			{
				var newUserRecipe = new UserRecipe
				{
					UserServer = userServer,
					Recipe = recipe
				};
				UserRecipes.Add(newUserRecipe);
			}
		}

		// Retourner la liste mise à jour des UserRecipes
		return UserRecipes;
	}



	public List<Recipe> GetAvailableRecipes(bool limitToSkillLevelRecipes = false, bool addNonSkilledRecipes = false)
	{
		var skills = UserSkills.Select(us => us.Skill);
		var recipes = new HashSet<Recipe>();
		
		foreach (var skill in skills)
		{
			var userSkillLevel = skill.UserSkills.First().Level;
			var foundRecipes = skill.Recipes.Where(r => r.Skill == skill);
			
			recipes.UnionWith(limitToSkillLevelRecipes ? foundRecipes.Where(r => r.SkillLevel <= userSkillLevel) : foundRecipes);
		}
		
		if (addNonSkilledRecipes)
		{
			recipes.UnionWith(UserRecipes.Select(ucr => ucr.Recipe).Where(r => r.Skill is null));
		}
		
		return recipes.ToList();
	}
	
	public List<Skill> GetAvailableSkills()
	{
		var userSkills = UserSkills.Select(us => us.Skill);
		
		return serverDataService.Skills.Where(s => !userSkills.Contains(s)).ToList();
	}
	
	public List<CraftingTable> GetAvailableCraftingTables()
	{
		var userCraftingTables = UserCraftingTables.Select(uct => uct.CraftingTable);
		
		return serverDataService.CraftingTables.Where(ct => !userCraftingTables.Contains(ct)).ToList();
	}
}