using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

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

	// Méthode pour mettre à jour les CraftingTables d'un utilisateur
	public List<UserCraftingTable> UpdateUserCraftingTables(UserServer userServer, List<CraftingTable> newCraftingTables)
	{
        // Charger les UserCraftingTables existantes pour cet utilisateur
        var existingUserCraftingTables = UserCraftingTables.Where(uct => uct.UserServer.Id == userServer.Id);

		var craftingTablesToRemove = existingUserCraftingTables.Where(uct => !newCraftingTables.Any(ct => ct == uct.CraftingTable)).ToList();

		foreach (var existingTable in craftingTablesToRemove)
		{
			UserCraftingTables.Remove(existingTable);
		}
		
		PluginModule defaultModule = userServer.Server.PluginModules.FirstOrDefault(pm => pm.Name.Equals("NoUpgrade"));
		
		// Ajouter les nouvelles CraftingTables qui ne sont pas déjà associées
		foreach (var craftingTable in newCraftingTables)
		{
			if (!existingUserCraftingTables.Any(uct => uct.CraftingTable.Id == craftingTable.Id))
			{
				var newUserCraftingTable = new UserCraftingTable
				{
					UserServer = userServer,
					CraftingTable = craftingTable,
					// Associer un Upgrade si nécessaire (ici, initialisation avec "no upgrade" par défaut)
					//UpgradeId = 5
					PluginModule = defaultModule
				};
				UserCraftingTables.Add(newUserCraftingTable);
			}
		}
		
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
		var userCraftingTables = UserCraftingTables.Select(us => us.CraftingTable);
		
		return serverDataService.CraftingTables.Where(c => !userCraftingTables.Contains(c)).ToList();
	}
}