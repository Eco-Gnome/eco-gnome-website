using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.AspNetCore.Hosting.Server;

namespace ecocraft.Services;

public class ContextService(
    EcoCraftDbContext dbContext,
    LocalStorageService localStorageService,
    ServerDbService serverDbService,
    ServerDataService serverDataService,
    UserServerDataService userServerDataService,
    UserDbService userDbService)
{
    public event Action? OnContextChanged;

    public List<Server> DefaultServers = [];

    public Server? CurrentServer { get; private set; }
    public UserServer? CurrentUserServer { get; private set; }
    public User? CurrentUser { get; private set; }
    public LanguageCode CurrentLanguageCode { get; private set; }

    public List<Server> AvailableServers
    {
        get { return DefaultServers.Concat(CurrentUser?.UserServers?.Select(cus => cus.Server) ?? []).Distinct().ToList(); }
    }

    public async Task updateCurrentUser()
    {
        await userDbService.UpdateAndSave(CurrentUser);
    }

	public async Task ChangeLanguage(LanguageCode languageCode)
    {
        CurrentLanguageCode = languageCode;
        await localStorageService.AddItem("LanguageCode", CurrentLanguageCode.ToString());

        OnContextChanged?.Invoke();
    }

    public async Task ChangeServer(Server? server)
    {
        var userServer = CurrentUser?.UserServers.Find(us => us.ServerId == server?.Id);

        CurrentUserServer = userServer;
        CurrentServer = server;

        await serverDataService.RetrieveServerData(CurrentServer);
        await userServerDataService.RetrieveUserData(CurrentUserServer);

        await localStorageService.AddItem("ServerId", CurrentServer?.Id.ToString() ?? "");

        OnContextChanged?.Invoke();
    }

    public async Task InitializeContext()
    {
        string? localUserId = await localStorageService.GetItem("UserId");

        if (!String.IsNullOrEmpty(localUserId))
        {
            var searchedUser = await userDbService.GetByIdAsync(new Guid(localUserId));

            if (searchedUser is not null)
            {
                CurrentUser = searchedUser;
            }
        }

        CurrentUser ??= await userDbService.AddAndSave(new User
        {
            Pseudo = "My Pseudo",
            SecretId = new Guid(),
            CreationDateTime = DateTime.UtcNow,
            SuperAdmin = await userDbService.CountUsers() == 0,
        });

        await localStorageService.AddItem("UserId", CurrentUser.Id.ToString());
        DefaultServers.AddRange(await serverDbService.GetAllDefaultAsync());

        var lastServerId = await localStorageService.GetItem("ServerId");
        Server? searchedServer = null;

        if (!string.IsNullOrEmpty(lastServerId))
        {
            searchedServer = await serverDbService.GetByIdAsync(new Guid(lastServerId));
            if (searchedServer is not null)
            {
                if (!CurrentUser.UserServers.Any(ue => ue.ServerId == searchedServer.Id))
                {
                    searchedServer = null;
                }
            }
        }

        if(searchedServer is null)
        {
            if (CurrentUser.UserServers.Count != 0)
            {
                searchedServer = CurrentUser.UserServers.First().Server;
            }
            else if (DefaultServers.Count != 0)
            {
                await JoinServer(DefaultServers.First());
                searchedServer = DefaultServers.First();
            }
        }

        if (searchedServer is not null)
        {
            CurrentServer = searchedServer;
            CurrentUserServer = CurrentUser.UserServers.First(us => us.Server == searchedServer);
			await serverDataService.RetrieveServerData(CurrentServer);
			await userServerDataService.RetrieveUserData(CurrentUserServer);
		}
  

        var languageCode = await localStorageService.GetItem("LanguageCode");

        if (!string.IsNullOrEmpty(languageCode))
        {
            Enum.TryParse(languageCode, out LanguageCode myStatus);
            CurrentLanguageCode = myStatus;
        }
        else
        {
            CurrentLanguageCode = LanguageCode.en_US;
        }

        OnContextChanged?.Invoke();
        // Don't know why, but the second one allows the MudSelect of servers to correctly display the selected server
        OnContextChanged?.Invoke();
    }

    public async Task JoinServer(Server server, bool isAdmin = false)
    {
        UserServer userServer = new UserServer
        {
            UserId = CurrentUser!.Id,
            ServerId = server.Id,
            IsAdmin = isAdmin,
            Pseudo = CurrentUser.Pseudo,
        };

        userServer.UserSettings.Add(new UserSetting
        {
            UserServer = userServer,
        });

        userServer.UserMargins.Add(new UserMargin
        {
            UserServer = userServer,
            Name = "Default margin",
            Margin = 20,
        });

        CurrentUser.UserServers.Add(userServer);
        await dbContext.SaveChangesAsync();
    }

	public async Task LeaveServer(UserServer userServerToLeave)
	{
		CurrentUser?.UserServers.Remove(userServerToLeave);
		await dbContext.SaveChangesAsync();
	}

	public async Task KickFromServer(UserServer userServerToKick)
	{
		userServerToKick.Server.UserServers.Remove(userServerToKick);
		await dbContext.SaveChangesAsync();
	}

	public async Task DeleteCurrentServer()
    {
        if (!CurrentUserServer!.IsAdmin)
        {
            return;
        }

        serverDbService.Delete(CurrentServer!);
        await dbContext.SaveChangesAsync();
        await ChangeServer(null);
    }

    public async Task RenounceAdmin()
    {
        CurrentUserServer!.IsAdmin = false;
        await dbContext.SaveChangesAsync();
    }

    public string GetTranslation(IHasLocalizedName? hasLocalizedName)
    {
        if (hasLocalizedName is null) return "BUG_NO_NAME";

        string translation;

        switch (CurrentLanguageCode)
        {
            case LanguageCode.en_US:
                translation = hasLocalizedName.LocalizedName.en_US;
                    break;
                case LanguageCode.fr:
                translation = hasLocalizedName.LocalizedName.fr;
                    break;
                case LanguageCode.es:
                translation = hasLocalizedName.LocalizedName.es;
                    break;
                case LanguageCode.de:
                    translation = hasLocalizedName.LocalizedName.de;
                    break;
                case LanguageCode.ko:
                    translation = hasLocalizedName.LocalizedName.ko;
                    break;
                case LanguageCode.pt_BR:
                    translation = hasLocalizedName.LocalizedName.pt_BR;
                    break;
                case LanguageCode.zh_Hans:
                    translation = hasLocalizedName.LocalizedName.zh_Hans;
                    break;
                case LanguageCode.ru:
                    translation = hasLocalizedName.LocalizedName.ru;
                    break;
                case LanguageCode.it:
                    translation = hasLocalizedName.LocalizedName.it;
                    break;
                case LanguageCode.pt_PT:
                    translation = hasLocalizedName.LocalizedName.pt_PT;
                    break;
                case LanguageCode.hu:
                    translation = hasLocalizedName.LocalizedName.hu;
                    break;
                case LanguageCode.ja:
                    translation = hasLocalizedName.LocalizedName.ja;
                    break;
                case LanguageCode.nn:
                    translation = hasLocalizedName.LocalizedName.nn;
                    break;
                case LanguageCode.pl:
                    translation = hasLocalizedName.LocalizedName.pl;
                    break;
                case LanguageCode.nl:
                    translation = hasLocalizedName.LocalizedName.nl;
                    break;
                case LanguageCode.ro:
                    translation = hasLocalizedName.LocalizedName.ro;
                    break;
                case LanguageCode.da:
                    translation = hasLocalizedName.LocalizedName.da;
                    break;
                case LanguageCode.cs:
                    translation = hasLocalizedName.LocalizedName.cs;
                    break;
                case LanguageCode.sv:
                    translation = hasLocalizedName.LocalizedName.sv;
                    break;
                case LanguageCode.uk:
                    translation = hasLocalizedName.LocalizedName.uk;
                    break;
                case LanguageCode.el:
                    translation = hasLocalizedName.LocalizedName.el;
                    break;
                case LanguageCode.ar_sa:
                    translation = hasLocalizedName.LocalizedName.ar_sa;
                    break;
                case LanguageCode.vi:
                    translation = hasLocalizedName.LocalizedName.vi;
                    break;
                case LanguageCode.tr:
                    translation = hasLocalizedName.LocalizedName.tr;
                    break;
                default:
                    throw new ArgumentException($"Unsupported LanguageCode: {CurrentLanguageCode}");
        }

        if (String.IsNullOrEmpty(translation))
        {
            translation = hasLocalizedName.LocalizedName.en_US;
        }

        return translation;
    }
}
