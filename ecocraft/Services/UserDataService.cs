using ecocraft.Models;

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

}