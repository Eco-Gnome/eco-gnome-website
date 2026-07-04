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
    UserSkillDbService userSkillDbService,
    UserTalentDbService userTalentDbService,
    UserCraftingTableDbService userCraftingTableDbService,
    UserPriceDbService userPriceDbService,
    UserRecipeDbService userRecipeDbService,
    UserElementDbService userElementDbService,
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

    // DataContext actuellement sélectionné dans le PriceCalculator. Exposé ici pour que le Header
    // puisse gater le bouton d'aide d'onboarding sur le nombre de métiers. Signal dédié (distinct de
    // OnContextChanged, qui déclenche un rechargement complet du PriceCalculator).
    public DataContext? CurrentDataContext { get; set; }
    public event Action? OnCurrentDataContextChanged;
    public void NotifyCurrentDataContextChanged() => OnCurrentDataContextChanged?.Invoke();

    // Demande d'ouverture de l'overlay d'aide d'onboarding (déclenchée depuis le Header, traitée par
    // le PriceCalculator qui rend l'overlay au-dessus de la page).
    public event Action? OnHelpOverlayRequested;
    public void RequestHelpOverlay() => OnHelpOverlayRequested?.Invoke();

    public List<Server> AvailableServers
    {
        get { return _defaultServers.Concat(CurrentUser?.UserServers.Select(cus => cus.Server) ?? []).DistinctBy(s => s.Id).ToList(); }
    }

    public async Task ChangeServer(Server server, bool isAdmin = false)
    {
        if (CurrentServer?.Id == server.Id)
        {
            return;
        }

        var userServer = CurrentUser!.UserServers.Find(us => us.ServerId == server.Id || us.Server.Id == server.Id);

        if (userServer == null)
        {
            await JoinServer(server, isAdmin);
            userServer = CurrentUser!.UserServers.Find(us => us.ServerId == server.Id);
        }

        // Rehydrate selected server with full members list to keep admin pages in sync
        // when switching from header without a full page reload.
        var serverWithUsers = await serverDbService.GetByIdAsync(server.Id);
        if (serverWithUsers is not null)
        {
            server.UserServers = serverWithUsers.UserServers;
            server.EcoServerId = serverWithUsers.EcoServerId;
            server.IsCalorieCostLocked = serverWithUsers.IsCalorieCostLocked;
            server.LockedCalorieCost = serverWithUsers.LockedCalorieCost;
            server.CalorieCostMin = serverWithUsers.CalorieCostMin;
            server.CalorieCostDefault = serverWithUsers.CalorieCostDefault;
            server.CalorieCostMax = serverWithUsers.CalorieCostMax;
            server.IsMarginLocked = serverWithUsers.IsMarginLocked;
            server.LockedMargin = serverWithUsers.LockedMargin;
            server.MarginMin = serverWithUsers.MarginMin;
            server.MarginDefault = serverWithUsers.MarginDefault;
            server.MarginMax = serverWithUsers.MarginMax;
            server.LastDataUploadTime = serverWithUsers.LastDataUploadTime;
            server.JoinCode = serverWithUsers.JoinCode;
            server.ApiKey = serverWithUsers.ApiKey;
            server.IsAutomationPlannerEnabled = serverWithUsers.IsAutomationPlannerEnabled;
        }

        // Force pages to reload server-scoped data instead of reusing previous server cache.
        CurrentServerData = null;
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
            await ChangeServer(searchedServer);
        }
    }

    public void InvokeContextChanged()
    {
		OnContextChanged?.Invoke();
	}

    public async Task JoinServer(Server server, bool isAdmin = false)
    {
        var existingUserServer = CurrentUser!.UserServers.Find(us => us.ServerId == server.Id || us.Server.Id == server.Id);

        if (existingUserServer is not null)
        {
            if (existingUserServer.DataContexts.Count == 0)
            {
                await AddDataContext(existingUserServer, true);
            }

            return;
        }

        var userServer = new UserServer
        {
            User = CurrentUser!,
            Server = server,
            IsAdmin = isAdmin,
            UserId = CurrentUser!.Id,
            ServerId = server.Id,
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
            CalorieCost = userServer.Server.IsCalorieCostLocked
                ? userServer.Server.LockedCalorieCost ?? userServer.Server.CalorieCostDefault ?? 0
                : userServer.Server.CalorieCostDefault ?? 0
        };

        dataContext.UserSettings.Add(userSetting);

        var userMargin = new UserMargin
        {
            DataContext = dataContext,
            Name = localizationService.GetTranslation("ContextService.DefaultMargin"),
            Margin = userServer.Server.IsMarginLocked
                ? userServer.Server.LockedMargin ?? userServer.Server.MarginDefault ?? 0
                : userServer.Server.MarginDefault ?? 20,
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

    public async Task<DataContext> DuplicateDataContext(DataContext source)
    {
        if (CurrentServerData is null)
        {
            throw new InvalidOperationException("CurrentServerData is not initialized.");
        }

        var src = await dataContextDbService.GetDataContextWithData(source.Id, CurrentServerData);

        var newCtx = new DataContext
        {
            Id = Guid.NewGuid(),
            UserServer = source.UserServer,
            UserServerId = source.UserServerId,
            Name = src.Name + localizationService.GetTranslation("DataContext.CopySuffix"),
            IsDefault = false,
            IsShoppingList = src.IsShoppingList,
        };

        var newSkills = new Dictionary<Guid, UserSkill>();
        var newTalents = new Dictionary<Guid, UserTalent>();
        var newTables = new Dictionary<Guid, UserCraftingTable>();
        var newSettings = new Dictionary<Guid, UserSetting>();
        var newMargins = new Dictionary<Guid, UserMargin>();
        var newRecipes = new Dictionary<Guid, UserRecipe>();
        var newElements = new Dictionary<Guid, UserElement>();
        var newPrices = new Dictionary<Guid, UserPrice>();

        foreach (var us in src.UserSkills)
        {
            newSkills[us.Id] = new UserSkill
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                Skill = us.Skill,
                Level = us.Level,
            };
        }

        foreach (var ut in src.UserTalents)
        {
            newTalents[ut.Id] = new UserTalent
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                Talent = ut.Talent,
                Level = ut.Level,
            };
        }

        foreach (var uct in src.UserCraftingTables)
        {
            newTables[uct.Id] = new UserCraftingTable
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                CraftingTable = uct.CraftingTable,
                PluginModule = uct.PluginModule,
                FuelItem = uct.FuelItem,
                AdditionalCraftMinuteFee = uct.AdditionalCraftMinuteFee,
                TotalCraftMinuteFee = uct.TotalCraftMinuteFee,
                SkilledPluginModules = uct.SkilledPluginModules.ToList(),
            };
        }

        foreach (var us in src.UserSettings)
        {
            newSettings[us.Id] = new UserSetting
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                MarginType = us.MarginType,
                CalorieCost = us.CalorieCost,
                DisplayNonSkilledRecipes = us.DisplayNonSkilledRecipes,
                OnlyLevelAccessibleRecipes = us.OnlyLevelAccessibleRecipes,
                ApplyMarginBetweenSkills = us.ApplyMarginBetweenSkills,
            };
        }

        foreach (var um in src.UserMargins)
        {
            newMargins[um.Id] = new UserMargin
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                Name = um.Name,
                Margin = um.Margin,
                Rounding = um.Rounding,
            };
        }

        foreach (var ur in src.UserRecipes)
        {
            newRecipes[ur.Id] = new UserRecipe
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                Recipe = ur.Recipe,
                RoundFactor = ur.RoundFactor,
                LockShare = ur.LockShare,
                CraftMinutesOverride = ur.CraftMinutesOverride,
            };
        }

        foreach (var ue in src.UserElements)
        {
            if (!newRecipes.TryGetValue(ue.UserRecipeId, out var newRecipe))
            {
                continue;
            }

            newElements[ue.Id] = new UserElement
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                Element = ue.Element,
                Price = ue.Price,
                IsMarginPrice = ue.IsMarginPrice,
                Share = ue.Share,
                IsReintegrated = ue.IsReintegrated,
                UserRecipe = newRecipe,
            };
        }

        foreach (var up in src.UserPrices)
        {
            newPrices[up.Id] = new UserPrice
            {
                Id = Guid.NewGuid(),
                DataContext = newCtx,
                ItemOrTag = up.ItemOrTag,
                Price = up.Price,
                MarginPrice = up.MarginPrice,
                OverrideIsBought = up.OverrideIsBought,
            };
        }

        // Rewire intra-context FKs once all dictionaries are populated.
        foreach (var ur in src.UserRecipes)
        {
            if (ur.ParentUserRecipeId is Guid parentId
                && newRecipes.TryGetValue(parentId, out var newParent)
                && newRecipes.TryGetValue(ur.Id, out var newUr))
            {
                newUr.ParentUserRecipe = newParent;
                newUr.ParentUserRecipeId = newParent.Id;
            }
        }

        foreach (var up in src.UserPrices)
        {
            if (!newPrices.TryGetValue(up.Id, out var newUp)) continue;

            if (up.PrimaryUserElementId is Guid peId
                && newElements.TryGetValue(peId, out var newPe))
            {
                newUp.PrimaryUserElement = newPe;
                newUp.PrimaryUserElementId = newPe.Id;
            }
            if (up.PrimaryUserPriceId is Guid ppId
                && newPrices.TryGetValue(ppId, out var newPp))
            {
                newUp.PrimaryUserPrice = newPp;
                newUp.PrimaryUserPriceId = newPp.Id;
            }
            if (up.UserMarginId is Guid umId
                && newMargins.TryGetValue(umId, out var newUm))
            {
                newUp.UserMargin = newUm;
                newUp.UserMarginId = newUm.Id;
            }
        }

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            dataContextDbService.Create(context, newCtx);
            foreach (var s in newSkills.Values)    userSkillDbService.Create(context, s);
            foreach (var t in newTalents.Values)   userTalentDbService.Create(context, t);
            foreach (var ct in newTables.Values)   userCraftingTableDbService.Create(context, ct);
            foreach (var st in newSettings.Values) userSettingDbService.Create(context, st);
            foreach (var m in newMargins.Values)   userMarginDbService.Create(context, m);
            foreach (var r in newRecipes.Values)   userRecipeDbService.Create(context, r);
            foreach (var e in newElements.Values)  userElementDbService.Create(context, e);
            foreach (var p in newPrices.Values)    userPriceDbService.Create(context, p);
            return Task.CompletedTask;
        });

        var tablesNeedingPluginModules = newTables.Values
            .Where(t => t.SkilledPluginModules.Count > 0)
            .ToList();
        if (tablesNeedingPluginModules.Count > 0)
        {
            await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
            {
                foreach (var ct in tablesNeedingPluginModules)
                {
                    await userCraftingTableDbService.UpdateAllAsync(context, ct);
                }
            });
        }

        CurrentUserServer!.DataContexts.Add(newCtx);
        return newCtx;
    }

    public async Task SetDefaultDataContext(DataContext target)
    {
        if (target.IsDefault || target.IsShoppingList) return;

        var previousDefault = CurrentUserServer!.DataContexts.FirstOrDefault(d => d.IsDefault && d.Id != target.Id);

        target.IsDefault = true;
        if (previousDefault is not null) previousDefault.IsDefault = false;

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            dataContextDbService.UpdateIsDefault(context, target);
            if (previousDefault is not null)
                dataContextDbService.UpdateIsDefault(context, previousDefault);
            return Task.CompletedTask;
        });
    }

	public async Task LeaveServer(UserServer userServerToLeave)
    {
        var server = userServerToLeave.Server;
        var serverId = server.Id;
        var remainingMembers = await userServerDbService.CountByServerIdAsync(serverId);
        var shouldDeleteServer = remainingMembers <= 1;

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            userServerDbService.Destroy(context, userServerToLeave);
            return Task.CompletedTask;
        });

        CurrentUser?.UserServers.Remove(userServerToLeave);
        CurrentServer = null;

        if (shouldDeleteServer)
        {
            _ = Task.Run(async () =>
            {
                await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
                {
                    await ClearTalentLocalizedDescriptionsForServer(context, serverId);
                    serverDbService.Destroy(context, new Server { Id = serverId });
                });
            });
        }
	}

	public async Task KickFromServer(UserServer userServerToKick)
	{
        var server = userServerToKick.Server;
        var serverId = server.Id;
        var remainingMembers = await userServerDbService.CountByServerIdAsync(serverId);
        var shouldDeleteServer = remainingMembers <= 1;

        await EcoCraftDbContext.ContextSaveAsync(factory, context =>
        {
            userServerDbService.Destroy(context, userServerToKick);
            return Task.CompletedTask;
        });

		userServerToKick.Server.UserServers.Remove(userServerToKick);

        if (shouldDeleteServer)
        {
            _ = Task.Run(async () =>
            {
                await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
                {
                    await ClearTalentLocalizedDescriptionsForServer(context, serverId);
                    serverDbService.Destroy(context, new Server { Id = serverId });
                });
            });
        }
	}

	public async Task DeleteCurrentServer()
    {
        if (CurrentUserServer is null || CurrentServer is null || CurrentUser is null)
        {
            return;
        }

        if (!CurrentUserServer.IsAdmin)
        {
            return;
        }

        var deletedServerId = CurrentServer.Id;

        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            await ClearTalentLocalizedDescriptionsForServer(context, deletedServerId);
            serverDbService.Destroy(context, CurrentServer!);
        });

        CurrentUser.UserServers.RemoveAll(us => us.ServerId == deletedServerId || us.Server.Id == deletedServerId);
        CurrentServerData = null;
        CurrentServer = null;
        CurrentUserServer = null;

        var server = CurrentUser.UserServers.FirstOrDefault()?.Server;

        if (server is not null)
        {
            await ChangeServer(CurrentUser.UserServers.First().Server);
            return;
        }

        await localStorageService.AddItem("ServerId", "");
        OnContextChanged?.Invoke();
    }

    public async Task DeleteServerAsSuperAdmin(Guid serverId)
    {
        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            await ClearTalentLocalizedDescriptionsForServer(context, serverId);
            serverDbService.Destroy(context, new Server { Id = serverId });
        });

        await HandleDeletedServer(serverId);
    }

    public async Task HandleDeletedServer(Guid serverId)
    {
        if (CurrentUser is null)
        {
            return;
        }

        _defaultServers.RemoveAll(s => s.Id == serverId);
        CurrentUser.UserServers.RemoveAll(us => us.ServerId == serverId || us.Server.Id == serverId);

        if (CurrentServer?.Id == serverId)
        {
            CurrentServerData = null;
            CurrentServer = null;
            CurrentUserServer = null;

            var nextServer = CurrentUser.UserServers.FirstOrDefault()?.Server;
            if (nextServer is not null)
            {
                await ChangeServer(nextServer);
                return;
            }

            await localStorageService.AddItem("ServerId", "");
        }

        OnContextChanged?.Invoke();
    }

    public async Task DeleteServersAsSuperAdmin(IReadOnlyCollection<Guid> serverIds)
    {
        if (serverIds.Count == 0)
        {
            return;
        }

        var ids = serverIds.ToArray();

        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            await ClearTalentLocalizedDescriptionsForServers(context, ids);
            await context.Servers.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync();
        });

        foreach (var id in ids)
        {
            await HandleDeletedServer(id);
        }
    }

    public async Task DeleteUsersAsSuperAdmin(IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var ids = userIds.ToArray();

        await EcoCraftDbContext.ContextSaveAsync(factory, async context =>
        {
            await context.Users.Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync();
        });
    }

    private static async Task ClearTalentLocalizedDescriptionsForServer(EcoCraftDbContext context, Guid serverId)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Talent"" AS t
            SET ""LocalizedDescriptionId"" = NULL
            FROM ""Skill"" AS s
            WHERE t.""SkillId"" = s.""Id""
              AND s.""ServerId"" = {serverId}
              AND t.""LocalizedDescriptionId"" IS NOT NULL;");
    }

    private static async Task ClearTalentLocalizedDescriptionsForServers(EcoCraftDbContext context, Guid[] serverIds)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Talent"" AS t
            SET ""LocalizedDescriptionId"" = NULL
            FROM ""Skill"" AS s
            WHERE t.""SkillId"" = s.""Id""
              AND s.""ServerId"" = ANY({serverIds})
              AND t.""LocalizedDescriptionId"" IS NOT NULL;");
    }
}
