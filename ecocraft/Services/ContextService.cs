﻿using ecocraft.Models;

namespace ecocraft.Services;

public class ContextService(
    LocalStorageService localStorageService,
    ServerDbService serverDbService,
    ServerDataService serverDataService,
    UserDataService userDataService,
    UserDbService userDbService)
{
    public event Action? OnContextChanged;

    public List<Server> DefaultServers = [];

    public Server? CurrentServer { get; private set; }
    public UserServer? CurrentUserServer { get; private set; }
    public User? CurrentUser { get; private set; }

    public List<Server> AvailableServers
    {
        get { return DefaultServers.Concat(CurrentUser?.UserServers?.Select(cus => cus.Server) ?? []).ToList(); }
    }

    public async Task ChangeServer(Server? server)
    {
        if (server is null)
        {
            return;
        }
        
        var userServer = CurrentUser?.UserServers.Find(us => us.ServerId == server.Id);

        if (userServer is null)
        {
            userServer = new UserServer()
            {
                ServerId = server.Id,
                UserId = CurrentUser.Id,
            };
            CurrentUser.UserServers.Add(userServer);
            await userDbService.UpdateAsync(CurrentUser);
        }

        CurrentUserServer = userServer;
        CurrentServer = server;

        await serverDataService.RetrieveServerData(CurrentServer);
        await userDataService.RetrieveUserData(CurrentUserServer);

        await localStorageService.AddItem("ServerId", CurrentServer.Id.ToString());

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

        if (CurrentUser is null)
        {
            CurrentUser = new User
            {
                Id = new Guid(),
                Pseudo = "John Doe",
                SecretId = new Guid(),
            };
            await userDbService.AddAsync(CurrentUser);
        }

        await localStorageService.AddItem("UserId", CurrentUser.Id.ToString());
        DefaultServers.AddRange(await serverDbService.GetAllDefaultAsync());

        var lastServerId = await localStorageService.GetItem("ServerId");

        if (!string.IsNullOrEmpty(lastServerId))
        {
            var searchedServer = await serverDbService.GetByIdAsync(new Guid(lastServerId));

            if (searchedServer is not null)
            {
                var foundUserServer = CurrentUser.UserServers.Find(ue => ue.ServerId == searchedServer.Id);

                if (foundUserServer is not null)
                {
                    CurrentServer = searchedServer;
                    CurrentUserServer = foundUserServer;
					await serverDataService.RetrieveServerData(CurrentServer);
					await userDataService.RetrieveUserData(CurrentUserServer);
				}
            }
        }

        OnContextChanged?.Invoke();
        // Don't know why, but the second one allows the MudSelect of servers to correctly display the selected server
        OnContextChanged?.Invoke();
    }
}