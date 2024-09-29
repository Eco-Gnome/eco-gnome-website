using ecocraft.Models;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

public class UserDataService(UserSkillDbService userSkillDbService,
    UserCraftingTableDbService userCraftingTableDbService,
    UserSettingDbService userSettingDbService,
    UserElementDbService userElementDbService,
    UserPriceDbService userPriceDbService)
{
    public List<UserSkill> UserSkills { get; private set; } = [];
    public List<UserCraftingTable> UserCraftingTables { get; private set; } = [];
    public List<UserSetting> UserSettings { get; private set; } = [];
    public List<UserElement> UserElements { get; private set; } = [];
    public List<UserPrice> UserPrices { get; private set; } = [];

    public async Task RetrieveUserData(UserServer userServer)
    {
        var userSkillsTask = userSkillDbService.GetByUserServerAsync(userServer);
        var userCraftingTablesTask = userCraftingTableDbService.GetByUserServerAsync(userServer);
        var userSettingsTask = userSettingDbService.GetByUserServerAsync(userServer);
        var userElementsTask = userElementDbService.GetByUserServerAsync(userServer);
        var userPricesTask = userPriceDbService.GetByUserServerAsync(userServer);
        
        await Task.WhenAll(userSkillsTask, userCraftingTablesTask, userSettingsTask, userElementsTask, userPricesTask);

        UserSkills = userSkillsTask.Result;
        UserCraftingTables = userCraftingTablesTask.Result;
        UserSettings = userSettingsTask.Result;
        UserElements = userElementsTask.Result;
        UserPrices = userPricesTask.Result;
    }

    public void AddUserSkill(UserSkill userSkill)
    {
		UserSkills.Add(userSkill);
	}

    public void RemoveUserSkill(UserSkill userSkill)
    {
        UserSkills.Remove(userSkill);
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
            UserCraftingTables.Remove(existingTable);

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

	// Méthode pour calculer les recettes disponibles
	public IEnumerable<Recipe> GetAvailableRecipes()
	{
		var skills = UserSkills.Select(us => us.Skill);
		var craftingTables = UserCraftingTables.Select(uct => uct.CraftingTable);
		var recipes = new HashSet<Recipe>();

		// Ajouter les recettes en fonction des compétences
		foreach (var skill in skills)
		{
			var userSkilllevel = UserSkills.FirstOrDefault(us => us.Skill == skill).Level;
			recipes.UnionWith(skill.Recipes.Where(r => r.SkillLevel <= userSkilllevel));
		}

		// Ajouter les recettes en fonction des tables d'artisanat
		/*foreach (var table in craftingTables)
		{
			recipes.UnionWith(table.Recipes);
		}*/

		return recipes;
	}

}