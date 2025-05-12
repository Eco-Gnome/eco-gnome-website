using ecocraft.Extensions;
using ecocraft.Models;
using ecocraft.Services.DbServices;

namespace ecocraft.Services;

public class ContextService(
    LocalizationService localizationService,
    EcoCraftDbContext dbContext,
    LocalStorageService localStorageService,
    ServerDbService serverDbService,
    DataContextDbService dataContextDbService,
    ServerDataService serverDataService,
    UserServerDataService userServerDataService,
    UserDbService userDbService)
{
    private readonly List<Server> _defaultServers = [];

    public event Action? OnContextChanged;
    public Server? CurrentServer { get; private set; }
    public UserServer? CurrentUserServer { get; private set; }
    public User? CurrentUser { get; private set; }

    public List<Server> AvailableServers
    {
        get { return _defaultServers.Concat(CurrentUser?.UserServers.Select(cus => cus.Server) ?? []).Distinct().ToList(); }
    }

    public async Task UpdateCurrentUser()
    {
        await userDbService.UpdateAndSave(CurrentUser!);
    }

    public async Task ChangeServer(Server server, bool isAdmin = false)
    {
        var userServer = CurrentUser!.UserServers.Find(us => us.ServerId == server.Id);

        if (userServer == null)
        {
            await JoinServer(server, isAdmin);
            userServer = CurrentUser!.UserServers.Find(us => us.ServerId == server.Id);
        }

        CurrentServer = server;
        CurrentUserServer = userServer;

        await localStorageService.AddItem("ServerId", CurrentServer?.Id.ToString() ?? "");

        await serverDataService.RetrieveServerData(null);
        await userServerDataService.RetrieveUserData(null);

        OnContextChanged?.Invoke();
    }

    public async Task InitializeUserContext()
    {
        var localUserId = await localStorageService.GetItem("UserId");
        var secretUserId = await localStorageService.GetItem("SecretUserId");

        if (!string.IsNullOrEmpty(localUserId))
        {
            var searchedUser = await userDbService.GetByIdAsync(new Guid(localUserId));

            if (searchedUser is not null)
            {
                // For Migration Purpose
                if (searchedUser.SecretId.ToString() == new Guid().ToString())
                {
                    searchedUser.SecretId = Guid.NewGuid();
                    secretUserId = searchedUser.SecretId.ToString();
                    await dbContext.SaveChangesAsync();
                }

                if (searchedUser.SecretId.ToString().Equals(secretUserId))
                {
                    CurrentUser = searchedUser;
                }
            }
        }

        var newUser = new User
        {
            SecretId = Guid.NewGuid(),
            CreationDateTime = DateTime.UtcNow,
            SuperAdmin = await userDbService.CountUsers() == 0,
        };
        newUser.GeneratePseudo();

        CurrentUser ??= await userDbService.AddAndSave(newUser);

        await localStorageService.AddItem("UserId", CurrentUser.Id.ToString());
        await localStorageService.AddItem("SecretUserId", CurrentUser.SecretId.ToString());

        var languageCode = await localStorageService.GetItem("LanguageCode");

        if (!string.IsNullOrEmpty(languageCode))
        {
            Enum.TryParse(languageCode, out LanguageCode myStatus);
            await localizationService.SetLanguageAsync(myStatus);
        }
        else
        {
            await localizationService.SetLanguageAsync(LanguageCode.en_US);
        }
    }

    public async Task InitializeServerContext()
    {
        _defaultServers.AddRange(await serverDbService.GetAllDefaultAsync());

        var lastServerId = await localStorageService.GetItem("ServerId");
        Server? searchedServer = null;

        if (!string.IsNullOrEmpty(lastServerId))
        {
            searchedServer = await serverDbService.GetByIdAsync(new Guid(lastServerId));
            if (searchedServer is not null)
            {
                if (CurrentUser!.UserServers.All(ue => ue.ServerId != searchedServer.Id))
                {
                    searchedServer = null;
                }
            }
        }

        if (searchedServer is null)
        {
            if (CurrentUser!.UserServers.Count != 0)
            {
                searchedServer = CurrentUser.UserServers.First().Server;
            }
            else if (_defaultServers.Count != 0)
            {
                await JoinServer(_defaultServers.First());
                searchedServer = _defaultServers.First();
            }
        }

        if (searchedServer is not null)
        {
            CurrentServer = searchedServer;
            CurrentUserServer = CurrentUser!.UserServers.First(us => us.Server == searchedServer);
        }
    }

    public void InvokeContextChanged()
    {
		OnContextChanged?.Invoke();
	}

    public async Task JoinServer(Server server, bool isAdmin = false)
    {
        var userServer = new UserServer
        {
            User = CurrentUser!,
            Server = server,
            IsAdmin = isAdmin,
        };

        CurrentUser!.UserServers.Add(userServer);

        await AddDataContext(userServer, true);
        await dbContext.SaveChangesAsync();
    }

    public async Task<DataContext> AddDataContext(UserServer userServer, bool isDefault = false)
    {
        var dataContext = new DataContext
        {
            Name = isDefault
                ? localizationService.GetTranslation("DataContext.DefaultContext")
                : localizationService.GetTranslation("DataContext.NewContext"),
            UserServer = userServer,
            IsDefault = isDefault,
        };

        dataContext.UserSettings.Add(new UserSetting
        {
            DataContext = dataContext,
        });

        dataContext.UserMargins.Add(new UserMargin
        {
            DataContext = dataContext,
            Name = localizationService.GetTranslation("ContextService.DefaultMargin"),
            Margin = 20,
        });

        dataContextDbService.Add(dataContext);
        await dbContext.SaveChangesAsync();

        return dataContext;
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

        await serverDbService.DeleteAsync(CurrentServer!, CurrentUser!);
        await dbContext.SaveChangesAsync();

        var server = CurrentUser!.UserServers.FirstOrDefault()?.Server;

        if (server is not null)
        {
            await ChangeServer(CurrentUser!.UserServers.First().Server);
        }
    }
}
