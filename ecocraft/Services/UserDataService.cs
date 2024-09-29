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
        var existingUserSkills = UserSkills.Where(us => us.UserServerId == userServer.Id);

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
                    Level = 0 // Ajuster le niveau si nécessaire
                };
                UserSkills.Add(newUserSkill);
            }
        }
        return UserSkills;
        //await _context.SaveChangesAsync();
    }

}