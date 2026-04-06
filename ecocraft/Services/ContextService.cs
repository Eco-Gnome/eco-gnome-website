using ecocraft.Extensions;
using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Services;

public class ContextService(
    IDbContextFactory<EcoCraftDbContext> factory,
    LocalStorageService localStorageService,
    LocalizationService localizationService,
    DataContextDbService dataContextDbService,
    UserMarginDbService userMarginDbService,
    UserSettingDbService userSettingDbService,
    ServerDbService serverDbService,
    UserDbService userDbService,
    UserServerDbService userServerDbService)
{
    private readonly List<Server> _defaultServers = [];

    public event Action? OnContextChanged;
    public Server? CurrentServer { get; private set; }
    public UserServer? CurrentUserServer { get; private set; }
    public User? CurrentUser { get; private set; }
    public Server? CurrentServerData { get; set; }

    public List<Server> AvailableServers
    {
        get { return _defaultServers.Concat(CurrentUser?.UserServers.Select(cus => cus.Server) ?? []).Distinct().ToList(); }
    }

    public async Task ChangeServer(Server server, bool isAdmin = false)
    {
        if (CurrentServer?.Id == server.Id)
        {
            return;
        }

        var userServer = CurrentUser!.UserServers.Find(us => us.ServerId == server.Id);

        if (userServer == null)
        {
            await JoinServer(server, isAdmin);
            userServer = CurrentUser!.UserServers.Find(us => us.ServerId == server.Id);
        }

        CurrentServer = server;
        CurrentUserServer = userServer;

        await localStorageService.AddItem("ServerId", CurrentServer?.Id.ToString() ?? "");

        OnContextChanged?.Invoke();
    }

    public async Task InitializeUserContext()
    {
        var localUserId = await localStorageService.GetItem("UserId");
        var secretUserId = await localStorageService.GetItem("SecretUserId");

        if (!string.IsNullOrEmpty(localUserId))
        {
            var searchedUser = await userDbService.GetByIdAndSecretAsync(new Guid(localUserId), new Guid(secretUserId));

            if (searchedUser is not null)
            {
                CurrentUser = searchedUser;
            }
        }

        if (CurrentUser is null)
        {
            var isFirstUser = await userDbService.CountUsers() == 0;

            var newUser = new User
            {
                SecretId = Guid.NewGuid(),
                CreationDateTime = DateTimeOffset.UtcNow,
                SuperAdmin = isFirstUser,
            };
            newUser.GeneratePseudo();

            await EcoCraftDbContext.ContextSaveAsync(factory, context =>
            {
                userDbService.Create(context, newUser);
                return Task.CompletedTask;
            });

            CurrentUser = newUser;

            if (isFirstUser)
            {
                var defaultServer = new Server
                {
                    Name = "Default",
                    IsDefault = true,
                    CreationDateTime = DateTimeOffset.UtcNow,
                };
                defaultServer.GenerateJoinCode();

                await EcoCraftDbContext.ContextSaveAsync(factory, context =>
                {
                    serverDbService.Create(context, defaultServer);
                    return Task.CompletedTask;
                });

                await JoinServer(defaultServer, isAdmin: true);
            }
        }

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
            CurrentUserServer = CurrentUser!.UserServers.First(us => us.Server.Id == searchedServer.Id);
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

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            userServerDbService.Create(context, userServer);
            return Task.CompletedTask;
        });

        CurrentUser!.UserServers.Add(userServer);

        await AddDataContext(userServer, true);
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

        var userSetting = new UserSetting
        {
            DataContext = dataContext,
        };

        dataContext.UserSettings.Add(userSetting);

        var userMargin = new UserMargin
        {
            DataContext = dataContext,
            Name = localizationService.GetTranslation("ContextService.DefaultMargin"),
            Margin = 20,
        };

        dataContext.UserMargins.Add(userMargin);

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            dataContextDbService.Create(context, dataContext);
            userSettingDbService.Create(context, userSetting);
            userMarginDbService.Create(context, userMargin);
            return Task.CompletedTask;
        });

        userServer.DataContexts.Add(dataContext);

        return dataContext;
    }

	public async Task LeaveServer(UserServer userServerToLeave)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            userServerDbService.Destroy(context, userServerToLeave);
            return Task.CompletedTask;
        });

        CurrentUser?.UserServers.Remove(userServerToLeave);
        CurrentServer = null;
	}

	public async Task KickFromServer(UserServer userServerToKick)
	{
        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            userServerDbService.Destroy(context, userServerToKick);
            return Task.CompletedTask;
        });

		userServerToKick.Server.UserServers.Remove(userServerToKick);
	}

	public async Task DeleteCurrentServer()
    {
        if (!CurrentUserServer!.IsAdmin)
        {
            return;
        }

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            serverDbService.Destroy(context, CurrentServer!);
            return Task.CompletedTask;
        });

        CurrentUser!.UserServers.Remove(CurrentUserServer!);
        CurrentServer = null;
        CurrentUserServer = null;

        var server = CurrentUser!.UserServers.FirstOrDefault()?.Server;

        if (server is not null)
        {
            await ChangeServer(CurrentUser!.UserServers.First().Server);
        }
    }
}
