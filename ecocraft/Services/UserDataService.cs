using ecocraft.Models;
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
    ServerDataService serverDataService)
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

    // Méthode pour mettre à jour les compétences de l'utilisateur
    public List<UserSkill> UpdateUserSkills(UserServer userServer, List<Skill> selectedSkills)
    {
        // Récupérer les compétences actuelles de l'utilisateur
        var existingUserSkills = UserSkills.Where(us => us.UserServer.Id == userServer.Id);

        // Supprimer les compétences non sélectionnées
        var skillsToRemove = existingUserSkills.Where(us => !selectedSkills.Any(s => s.Id == us.SkillId)).ToList();
        foreach(var userSkill in skillsToRemove)
            UserSkills.Remove(userSkill);            

        // Ajouter les nouvelles compétences sélectionnées
        foreach (var skill in selectedSkills)
        {
            if (!existingUserSkills.Any(us => us.SkillId == skill.Id))
            {
                var newUserSkill = new UserSkill
                {
                    UserServer = userServer,
                    Skill = skill,
                    Level = 1 // Ajuster le niveau si nécessaire
                };
                UserSkills.Add(newUserSkill);
            }
        }
        return UserSkills;
        //await _context.SaveChangesAsync();
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